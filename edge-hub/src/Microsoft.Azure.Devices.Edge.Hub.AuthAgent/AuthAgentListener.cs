// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.AuthAgent
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;

    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    public class AuthAgentListener : IProtocolHead
    {
        private readonly IAuthenticator authenticator;
        private readonly IUsernameParser usernameParser;
        private readonly IClientCredentialsFactory clientCredentialsFactory;

        private HttpListener listener;

        private Task listenerTask;
        private bool exitListenerLoop;

        // FIXME: should come from config
        private static readonly string listeningAddress = "http://localhost:7120/authenticate/";
        private const int maxInBufferSize = 32 * 1024;

        public AuthAgentListener(IAuthenticator authenticator, IUsernameParser usernameParser, IClientCredentialsFactory clientCredentialsFactory)
        {
            this.authenticator = authenticator;
            this.usernameParser = usernameParser;
            this.clientCredentialsFactory = clientCredentialsFactory;
        }

        public string Name => "AUTH";

        public Task StartAsync()
        {
            Events.Starting();
            this.Start();
            Events.Started();

            return Task.CompletedTask;
        }

        public async Task CloseAsync(CancellationToken token)
        {
            Events.Closing();

            var currentListener = Interlocked.Exchange(ref this.listener, null);
            if (currentListener == null)
            {
                Events.ClosedWhenNotRunning();
                return;
            }

            // this disposes the instance making the listener loop exit
            Volatile.Write(ref exitListenerLoop, true);
            currentListener.Close();

            await this.listenerTask;
            this.listenerTask = null;

            Events.Closed();
        }

        public void Dispose() => this.CloseAsync(CancellationToken.None).Wait();

        private void Start()
        {
            var newListener = new HttpListener();

            if (Interlocked.CompareExchange(ref this.listener, newListener, null) != null)
            {
                newListener.Close();
                Events.StartedWhenAlreadyRunning();
                throw new InvalidOperationException("Cannot start AuthAgent twice");
            }

            Volatile.Write(ref this.exitListenerLoop, false);

            this.listener.Prefixes.Add(listeningAddress);
            this.listener.Start();

            this.listenerTask = ListenerLoop();
            this.listenerTask.ContinueWith(_ => Restart());
        }

        private void Restart()
        {
            // no need for restart, stopped on purpose
            if (Volatile.Read(ref exitListenerLoop))
            {
                return;
            }

            var prevListener = Interlocked.Exchange(ref this.listener, null);
            prevListener?.Close();

            Events.Restarting();
            Start();
        }

        private async Task ListenerLoop()
        {
            while (true)
            {
                try
                {
                    var context = await this.listener.GetContextAsync();

                    Events.AuthRequestReceived();

                    var requestString = await ReadRequestBodyAsStringAsync(context.Request);
                    var requestRecord = this.Decode(requestString);

                    await requestRecord.Match(
                        async r => await this.SendResponseAsync(r, context),
                        async () => await this.SendErrorAsync(context)
                    );                   
                }
                catch (ObjectDisposedException e) when (e.ObjectName == typeof(HttpListener).FullName)
                {
                    Events.ShutdownListenerLoop();
                    break;
                }
                catch (Exception e)
                {
                    Events.ErrorExitListenerLoop(e);
                    break;
                }
            }
        }

        private async Task SendResponseAsync(AuthRequest request, HttpListenerContext context)
        {
            try
            {
                (var isAuthenticated, var credentials) =
                    await GetIdsFromUsername(request)
                            .FlatMap(ids => GetCredentials(request, ids.deviceId, ids.moduleId, ids.deviceClientType))
                            .Match(
                                async creds => (await AuthenticateAsync(creds), Option.Some(creds)),
                                () => Task.FromResult((false, Option.None<IClientCredentials>())));

                await SendAuthResultAsync(context, isAuthenticated, credentials);
            }
            catch (Exception e)
            {
                Events.ErrorSendingResponse(e);
                return;
            }
        }

        private Option<(string deviceId, string moduleId, string deviceClientType)> GetIdsFromUsername(AuthRequest request)
        {
            try
            {
                return Option.Some(this.usernameParser.Parse(request.Username));
            }
            catch (Exception e)
            {
                Events.InvalidUsernameFormat(e);
                return Option.None<(string, string, string)>();
            }
        }

        private Option<IClientCredentials> GetCredentials(AuthRequest request, string deviceId, string moduleId, string deviceClientType)
        {
            var result = Option.None<IClientCredentials>();

            try
            {
                if (!String.IsNullOrWhiteSpace(request.Password))
                {
                    result = Option.Some(this.clientCredentialsFactory.GetWithSasToken(deviceId, moduleId, deviceClientType, request.Password, false));
                }
                else if (!String.IsNullOrWhiteSpace(request.EncodedCertificate))
                {
                    var certificate = DecodeCertificate(request.EncodedCertificate);
                    var chain = DecodeCertificateChain(request.EncodedCertificateChain);

                    result = Option.Some(this.clientCredentialsFactory.GetWithX509Cert(deviceId, moduleId, deviceClientType, certificate, chain));
                }
                else
                {
                    Events.NoCredentialsSpecified();                    
                }
            }
            catch (Exception e)
            {
                Events.InvalidCredentials(e);
            }

            return result;
        }

        private async Task<bool> AuthenticateAsync(IClientCredentials credentials)
        {
            try
            {
                return await this.authenticator.AuthenticateAsync(credentials);                
            }
            catch (Exception e)
            {
                Events.InvalidCredentials(e);                               
            }

            return false;
        }

        private Task SendAuthResultAsync(HttpListenerContext context, bool isAuthenticated, Option<IClientCredentials> credentials)
        {
            // note, that if authenticated, then these values are present, and defaults never apply
            var iotHubName = credentials.Map(c => c.Identity.IotHubHostName).GetOrElse("any");
            var id = credentials.Map(c => c.Identity.Id).GetOrElse("anonymous");

            if (isAuthenticated)
            {
                Events.AuthSucceeded(id);
                return SendAsync(context, $@"{{""result"":200,""identity"":""{iotHubName}/{id}"",""version"":""2020-04-20""}}");
                
            }
            else
            {
                Events.AuthFailed(id);
                return SendErrorAsync(context);                
            }
        }

        private Task SendErrorAsync(HttpListenerContext context)
        {
            // we don't reveal error details: for the caller if something is not right, that is unauthenticated
            return SendAsync(context, @"{""result"":403,""version"":""2020-04-20""}");
        }

        private async Task SendAsync(HttpListenerContext context, string body)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(body);

            context.Response.ContentType = "application/json; charset=utf-8";
            context.Response.ContentEncoding = Encoding.UTF8;

            // we could set content-length, however it is not necessary.
            // leaving this here just for reference: response.ContentLength64 = buffer.Length;
            using (context.Response.OutputStream)
            {
                await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            }
        }

        private async Task<Option<string>> ReadRequestBodyAsStringAsync(HttpListenerRequest request)
        {
            var requestBodyBuffer = GetRequestBodyBuffer(request);

            var result = default(string);
            var bytesRead = default(int);

            using (var inputStream = request.InputStream)
            {
                try
                {
                    // Note, that if the caller decides to send the request in multiple tcp packets and they arrive
                    // slow, this call might not deliver the full content. Consider adding a loop counting the bracket "balance",
                    // given that this should be a json packet.
                    bytesRead = await request.InputStream.ReadAsync(requestBodyBuffer, 0, requestBodyBuffer.Length);
                }
                catch (Exception e)
                {
                    Events.ErrorReadingStream(e);
                    return Option.None<String>();
                }

                // read as many bytes as the buffer capacity. Remember that we allocated +1 byte,
                // so we don't expect that the buffer will be full. If it is, the Content-Length was
                // wrong, or the request is bigger than the maximum allowed.
                if (bytesRead == requestBodyBuffer.Length)
                {
                    Events.InvalidRequest();
                    return Option.None<String>();
                }

                try
                {
                    result = Encoding.UTF8.GetString(requestBodyBuffer, 0, bytesRead);
                }
                catch (Exception e)
                {
                    Events.ErrorDecodingStream(e);
                    return Option.None<String>();
                }
            }

            return Option.Some<String>(result);
        }

        private byte[] GetRequestBodyBuffer(HttpListenerRequest request)
        {
            // the request may contain the content-length in the header - or not
            var requestSize = request.ContentLength64 >= 0 ? request.ContentLength64 : maxInBufferSize;

            // if the client sends too big number in the header - don't believe it
            // also, overallocate the buffer by one, so at the end we should read less bytes than requested
            requestSize = Math.Min(maxInBufferSize, requestSize + 1);
            return new byte[requestSize];
        }

        private Option<AuthRequest> Decode(Option<string> request)
        {            
            try
            {
                return request.Map(s => JsonConvert.DeserializeObject<AuthRequest>(s));                
            }
            catch (Exception e)
            {
                Events.ErrorDecodingPayload(e);
                return Option.None<AuthRequest>();
            }
        }

        private X509Certificate2 DecodeCertificate(string encodedCertificate)
        {            
            try
            {
                var certificateContent = Convert.FromBase64String(encodedCertificate);
                var certificate = new X509Certificate2(certificateContent);

                return certificate;
            }
            catch (Exception e)
            {
                Events.ErrorDecodingCertificate(e);
                throw;
            }
        }

        private List<X509Certificate2> DecodeCertificateChain(string[] encodedCertificates)
        {
            if (encodedCertificates != null)
            {
                return encodedCertificates.Select(DecodeCertificate).ToList();
            }
            else
            {
                return new List<X509Certificate2>();
            }                
        }

        static class Events
        {
            const int IdStart = AuthAgentEventIds.AuthAgentListener;
            static readonly ILogger Log = Logger.Factory.CreateLogger<AuthAgentListener>();

            enum EventIds
            {
                Starting = IdStart,
                Restarting,
                Started,
                Closing,
                Closed,
                ClosedWhenNotRunning,
                StartedWhenAlreadyRunning,
                ShutdownListenerLoop,
                ErrorExitListenerLoop,
                AuthRequestReceived,
                AuthSucceeded,
                AuthFailed,
                InvalidRequest,
                ErrorReadingStream,
                ErrorDecodingStream,
                ErrorDecodingPayload,
                ErrorDecodingCertificate,
                NoCredentialsSpecified,
                InvalidUsernameFormat,
                InvalidCredentials,
                ErrorSendingResponse
            }

            public static void Starting()
            {
                Log.LogInformation((int)EventIds.Starting, "Starting AUTH head");
            }

            public static void Restarting()
            {
                Log.LogInformation((int)EventIds.Restarting, "Restarting AUTH head after error");
            }

            public static void Started()
            {
                Log.LogInformation((int)EventIds.Started, "Started AUTH head");
            }

            public static void Closing()
            {
                Log.LogInformation((int)EventIds.Closing, "Closing AUTH head");
            }

            public static void Closed()
            {
                Log.LogInformation((int)EventIds.Closed, "Closed AUTH head");
            }

            public static void ClosedWhenNotRunning()
            {
                Log.LogInformation((int)EventIds.ClosedWhenNotRunning, "Closed AUTH head when it was not running");
            }

            public static void StartedWhenAlreadyRunning()
            {
                Log.LogWarning((int)EventIds.StartedWhenAlreadyRunning, "Started AUTH head when it was already running");
            }

            public static void ShutdownListenerLoop()
            {
                Log.LogInformation((int)EventIds.ShutdownListenerLoop, "Exiting AUTH listener loop by closing protocol head");
            }

            public static void ErrorExitListenerLoop(Exception e)
            {
                Log.LogError((int)EventIds.ErrorExitListenerLoop, e, "Exiting AUTH listener loop by error");
            }

            public static void AuthRequestReceived()
            {
                Log.LogInformation((int)EventIds.AuthRequestReceived, "AUTH request received");
            }

            public static void AuthSucceeded(string id)
            {
                Log.LogInformation((int)EventIds.AuthSucceeded, "AUTH succeeded {0}", id);
            }

            public static void AuthFailed(string id)
            {
                Log.LogWarning((int)EventIds.AuthSucceeded, "AUTH failed {0}", id);
            }

            public static void InvalidRequest()
            {
                Log.LogWarning((int)EventIds.InvalidRequest, "Invalid request for AUTH - ignoring");
            }

            public static void ErrorReadingStream(Exception e)
            {
                Log.LogError((int)EventIds.ErrorReadingStream, e, "Error reading AUTH request");
            }

            public static void ErrorDecodingStream(Exception e)
            {
                Log.LogWarning((int)EventIds.ErrorDecodingStream, e, "Error decoding AUTH request, UTF8 encoding expected");
            }

            public static void ErrorDecodingPayload(Exception e)
            {
                Log.LogWarning((int)EventIds.ErrorDecodingPayload, e, "Error decoding AUTH request, invalid JSON structure");
            }

            public static void ErrorDecodingCertificate(Exception e)
            {
                Log.LogWarning((int)EventIds.ErrorDecodingCertificate, e, "Error decoding certificate");
            }

            public static void NoCredentialsSpecified()
            {
                Log.LogWarning((int)EventIds.NoCredentialsSpecified, "No credentials specified: either a certificate or a SAS token must be present for AUTH");
            }

            public static void InvalidUsernameFormat(Exception e)
            {
                Log.LogWarning((int)EventIds.InvalidUsernameFormat, e, "Invalid username format provided for AUTH");
            }

            public static void InvalidCredentials(Exception e)
            {
                Log.LogWarning((int)EventIds.InvalidCredentials, e, "Invalid credentials provided for AUTH");
            }

            public static void ErrorSendingResponse(Exception e)
            {
                Log.LogError((int)EventIds.ErrorSendingResponse, e, "Error sending AUTH response");
            }            
        }
    }
}
