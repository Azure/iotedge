// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http
{
    using System;
    using System.Security.Authentication;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Hub.Http.Extensions;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class HttpProxiedCertificateExtractor : IHttpProxiedCertificateExtractor
    {
        readonly IAuthenticator authenticator;
        readonly IClientCredentialsFactory identityFactory;
        readonly string iotHubName;
        readonly string edgeDeviceId;
        readonly string proxyModuleId;

        public HttpProxiedCertificateExtractor(
            IAuthenticator authenticator,
            IClientCredentialsFactory identityFactory,
            string iotHubName,
            string edgeDeviceId,
            string proxyModuleId)
        {
            this.authenticator = Preconditions.CheckNotNull(authenticator, nameof(authenticator));
            this.identityFactory = Preconditions.CheckNotNull(identityFactory, nameof(identityFactory));
            this.iotHubName = Preconditions.CheckNonWhiteSpace(iotHubName, nameof(iotHubName));
            this.edgeDeviceId = Preconditions.CheckNonWhiteSpace(edgeDeviceId, nameof(edgeDeviceId));
            this.proxyModuleId = Preconditions.CheckNonWhiteSpace(proxyModuleId, nameof(proxyModuleId));
        }

        public async Task<Option<X509Certificate2>> GetClientCertificate(HttpContext context)
        {
            X509Certificate2 clientCertificate;

            try
            {
                clientCertificate = context.GetForwardedCertificate();

                if (clientCertificate != null)
                {
                    Events.AuthenticationApiProxy(context.Connection.RemoteIpAddress.ToString());
                    // If the connection came through the API proxy, the client cert
                    // would have been forwarded in a custom header. But since TLS
                    // termination occurs at the proxy, we can only trust this custom
                    // header if the request came through authenticated api proxy.
                    await this.AuthenticateProxy(context);

                    Events.AuthenticateApiProxySuccess();
                }
            }
            catch (Exception ex)
            {
                throw new AuthenticationException($"Unable to authorize proxy {this.proxyModuleId} to forward device certificate - {ex.Message}");
            }

            return Option.Maybe(clientCertificate);
        }

        async Task AuthenticateProxy(HttpContext context)
        {
            string proxySasToken = this.GetSasTokenFromAuthHeader(context);
            IClientCredentials clientCredentials = this.identityFactory.GetWithSasToken(this.edgeDeviceId, this.proxyModuleId, string.Empty, proxySasToken, false, Option.None<string>(), Option.None<string>());

            if (!await this.authenticator.AuthenticateAsync(clientCredentials))
            {
                throw new AuthenticationException($"Unable to authenticate proxy {this.proxyModuleId} to forward certificate");
            }
        }

        string GetSasTokenFromAuthHeader(HttpContext context)
        {
            string authHeader = context.GetAuthHeader();

            SharedAccessSignature sharedAccessSignature;
            try
            {
                sharedAccessSignature = SharedAccessSignature.Parse(this.iotHubName, authHeader);
            }
            catch (Exception ex)
            {
                Events.AuthenticateApiProxyFailed(ex);
                throw new AuthenticationException($"Cannot parse SharedAccessSignature because of the following error - {ex.Message}");
            }

            return authHeader;
        }
    }

    static class Events
    {
        const int IdStart = HttpEventIds.HttpProxiedCertificateExtractor;
        static readonly ILogger Log = Logger.Factory.CreateLogger<HttpProxiedCertificateExtractor>();

        enum EventIds
        {
            AuthenticationApiProxy = IdStart,
            AuthenticationSuccess,
            AuthenticationFailed
        }

        public static void AuthenticationApiProxy(string remoteAddress)
        {
            Log.LogDebug((int)EventIds.AuthenticationApiProxy, $"Received authentication attempt through ApiProxy for {remoteAddress}");
        }

        public static void AuthenticateApiProxySuccess()
        {
            Log.LogDebug((int)EventIds.AuthenticationSuccess, $"Authentication attempt through ApiProxy success");
        }

        public static void AuthenticateApiProxyFailed(Exception ex)
        {
            Log.LogError((int)EventIds.AuthenticationFailed, $"Failed to authentication ApiProxy - {ex.Message}", ex);
        }
    }
}
