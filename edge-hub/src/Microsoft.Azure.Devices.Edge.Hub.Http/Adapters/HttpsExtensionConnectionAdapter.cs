// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Http.Adapters
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Security;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http.Features;
    using Microsoft.AspNetCore.Server.Kestrel.Core.Adapter.Internal;
    using Microsoft.AspNetCore.Server.Kestrel.Https;
    using Microsoft.Azure.Devices.Edge.Hub.Http.Extensions;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    // https://github.com/aspnet/HttpAbstractions/issues/808
    public class HttpsExtensionConnectionAdapter : IConnectionAdapter
    {
        // See http://oid-info.com/get/1.3.6.1.5.5.7.3.1
        // Indicates that a certificate can be used as a SSL server certificate
        const string ServerAuthenticationOid = "1.3.6.1.5.5.7.3.1";
        internal const string DisableHandshakeTimeoutSwitch = "Switch.Microsoft.AspNetCore.Server.Kestrel.Https.DisableHandshakeTimeout";
        static readonly TimeSpan HandshakeTimeout = TimeSpan.FromSeconds(10);
        static readonly ClosedAdaptedConnection ClosedAdaptedConnectionInstance = new ClosedAdaptedConnection();
        readonly HttpsConnectionAdapterOptions options;
        readonly X509Certificate2 serverCertificate;

        public HttpsExtensionConnectionAdapter(HttpsConnectionAdapterOptions options)
        {
            this.options = Preconditions.CheckNotNull(options, nameof(options));
            this.serverCertificate = Preconditions.CheckNotNull(options.ServerCertificate, nameof(options.ServerCertificate));
            EnsureCertificateIsAllowedForServerAuth(this.serverCertificate);
        }

        public bool IsHttps => true;

        public Task<IAdaptedConnection> OnConnectionAsync(ConnectionAdapterContext context) =>
            Task.Run(() => this.InnerOnConnectionAsync(context));

        async Task<IAdaptedConnection> InnerOnConnectionAsync(ConnectionAdapterContext context)
        {
            SslStream sslStream;
            bool certificateRequired;

            IList<X509Certificate2> chainElements = new List<X509Certificate2>();

            if (this.options.ClientCertificateMode == ClientCertificateMode.NoCertificate)
            {
                sslStream = new SslStream(context.ConnectionStream);
                certificateRequired = false;
            }
            else
            {
                sslStream = new SslStream(
                    context.ConnectionStream,
                    leaveInnerStreamOpen: false,
                    userCertificateValidationCallback: (sender, certificate, chain, sslPolicyErrors) =>
                    {
                        if (certificate == null)
                        {
                            return this.options.ClientCertificateMode != ClientCertificateMode.RequireCertificate;
                        }

                        if (this.options.ClientCertificateValidation == null)
                        {
                            if (sslPolicyErrors != SslPolicyErrors.None)
                            {
                                return false;
                            }
                        }
                        else if (!this.options.ClientCertificateValidation(new X509Certificate2(certificate), chain, sslPolicyErrors))
                        {
                            return false;
                        }

                        foreach (X509ChainElement element in chain.ChainElements)
                        {
                            chainElements.Add(element.Certificate);
                        }

                        return true;
                    });

                certificateRequired = true;
            }

            try
            {
                if (AppContext.TryGetSwitch(DisableHandshakeTimeoutSwitch, out bool handshakeDisabled) && handshakeDisabled)
                {
                    await sslStream.AuthenticateAsServerAsync(
                        this.serverCertificate,
                        certificateRequired,
                        this.options.SslProtocols,
                        this.options.CheckCertificateRevocation);
                }
                else
                {
                    try
                    {
                        Task handshakeTask = sslStream.AuthenticateAsServerAsync(
                            this.serverCertificate,
                            certificateRequired,
                            this.options.SslProtocols,
                            this.options.CheckCertificateRevocation);
                        Task handshakeTimeoutTask = Task.Delay(HandshakeTimeout);

                        Task firstTask = await Task.WhenAny(handshakeTask, handshakeTimeoutTask);

                        if (firstTask == handshakeTimeoutTask)
                        {
                            Events.AuthenticationTimedOut();

                            // Observe any exception that might be raised from AuthenticateAsServerAsync after the timeout.
                            ObserveTaskException(handshakeTask);

                            // This will cause the request processing loop to exit immediately and close the underlying connection.
                            sslStream.Dispose();
                            return ClosedAdaptedConnectionInstance;
                        }

                        // Observe potential handshake failures.
                        await handshakeTask;
                    }
                    catch (OperationCanceledException)
                    {
                        Events.AuthenticationTimedOut();
                        sslStream.Dispose();
                        return ClosedAdaptedConnectionInstance;
                    }
                }
            }
            catch (Exception)
            {
                Events.AuthenticationFailed();
                sslStream.Dispose();
                return ClosedAdaptedConnectionInstance;
            }

            Events.AuthenticationSuccess();

            // Always set the feature even though the cert might be null
            X509Certificate2 cert = sslStream.RemoteCertificate != null ? new X509Certificate2(sslStream.RemoteCertificate) : null;

            context.Features.Set<ITlsConnectionFeature>(
                new TlsConnectionFeature
                {
                    ClientCertificate = cert
                });

            context.Features.Set<ITlsConnectionFeatureExtended>(
                new TlsConnectionFeatureExtended
                {
                    ChainElements = chainElements
                });

            return new HttpsAdaptedConnection(sslStream);
        }

        static void EnsureCertificateIsAllowedForServerAuth(X509Certificate2 certificate)
        {
            /* If the Extended Key Usage extension is included, then we check that the serverAuth usage is included. (http://oid-info.com/get/1.3.6.1.5.5.7.3.1)
             * If the Extended Key Usage extension is not included, then we assume the certificate is allowed for all usages.
             * 
             * See also https://blogs.msdn.microsoft.com/kaushal/2012/02/17/client-certificates-vs-server-certificates/
             * 
             * From https://tools.ietf.org/html/rfc3280#section-4.2.1.13 "Certificate Extensions: Extended Key Usage"
             * 
             * If the (Extended Key Usage) extension is present, then the certificate MUST only be used
             * for one of the purposes indicated.  If multiple purposes are
             * indicated the application need not recognize all purposes indicated,
             * as long as the intended purpose is present.  Certificate using
             * applications MAY require that a particular purpose be indicated in
             * order for the certificate to be acceptable to that application.
             */

            bool hasEkuExtension = false;

            foreach (X509EnhancedKeyUsageExtension extension in certificate.Extensions.OfType<X509EnhancedKeyUsageExtension>())
            {
                hasEkuExtension = true;
                foreach (Oid oid in extension.EnhancedKeyUsages)
                {
                    if (oid.Value.Equals(ServerAuthenticationOid, StringComparison.Ordinal))
                    {
                        return;
                    }
                }
            }

            if (hasEkuExtension)
            {
                throw new InvalidOperationException("InvalidServerCertificateEku");
            }
        }

        static void ObserveTaskException(Task task) => _ = task.ContinueWith(t => _ = t.Exception, TaskScheduler.Current);

        class HttpsAdaptedConnection : IAdaptedConnection
        {
            readonly SslStream sslStream;

            public HttpsAdaptedConnection(SslStream sslStream)
            {
                this.sslStream = sslStream;
            }

            public Stream ConnectionStream => this.sslStream;

            public void Dispose() => this.sslStream.Dispose();
        }

        class ClosedAdaptedConnection : IAdaptedConnection
        {
            public Stream ConnectionStream { get; } = new ClosedStream();

            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2213:DisposableFieldsShouldBeDisposed", Justification = "Field does not need to be disposed.")]
            public void Dispose()
            {
            }
        }

        internal class ClosedStream : Stream
        {
            static readonly Task<int> ZeroResultTask = Task.FromResult(result: 0);

            public override bool CanRead => true;

            public override bool CanSeek => false;

            public override bool CanWrite => false;

            public override long Length
            {
                get { throw new NotSupportedException(); }
            }

            public override long Position
            {
                get { throw new NotSupportedException(); }
                set { throw new NotSupportedException(); }
            }

            public override void Flush()
            {
            }

            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

            public override void SetLength(long value) => throw new NotSupportedException();

            public override int Read(byte[] buffer, int offset, int count) => 0;

            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => ZeroResultTask;

            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<HttpsExtensionConnectionAdapter>();
            const int IdStart = HttpEventIds.HttpsExtensionConnectionAdapter;

            enum EventIds
            {
                AuthenticationTimedOut = IdStart,
                AuthenticationFailed,
                AuthenticationSuccess
            }

            public static void AuthenticationTimedOut() =>
                Log.LogInformation((int)EventIds.AuthenticationTimedOut, "HttpExtensionConnectionAdapter authentication timeout");

            public static void AuthenticationFailed() =>
                Log.LogInformation((int)EventIds.AuthenticationFailed, "HttpExtensionConnectionAdapter authentication failed");

            public static void AuthenticationSuccess() =>
                Log.LogDebug((int)EventIds.AuthenticationSuccess, "HttpExtensionConnectionAdapter authentication succeeded");
        }
    }
}
