// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Security.Authentication;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Hub.Http.Extensions;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class HttpRequestAuthenticator : IHttpRequestAuthenticator
    {
        readonly IAuthenticator authenticator;
        readonly IClientCredentialsFactory identityFactory;
        readonly string iotHubName;
        readonly IHttpProxiedCertificateExtractor httpProxiedCertificateExtractor;

        public HttpRequestAuthenticator(
            IAuthenticator authenticator,
            IClientCredentialsFactory identityFactory,
            string iotHubName,
            IHttpProxiedCertificateExtractor httpProxiedCertificateExtractor)
        {
            this.authenticator = Preconditions.CheckNotNull(authenticator, nameof(authenticator));
            this.identityFactory = Preconditions.CheckNotNull(identityFactory, nameof(identityFactory));
            this.iotHubName = Preconditions.CheckNonWhiteSpace(iotHubName, nameof(iotHubName));
            this.httpProxiedCertificateExtractor = Preconditions.CheckNotNull(httpProxiedCertificateExtractor, nameof(httpProxiedCertificateExtractor));
        }

        public async Task<HttpAuthResult> AuthenticateAsync(string actorDeviceId, Option<string> actorModuleId, Option<string> authChain, HttpContext context)
        {
            Preconditions.CheckNonWhiteSpace(actorDeviceId, nameof(actorDeviceId));
            Preconditions.CheckNotNull(context, nameof(context));

            IClientCredentials clientCredentials;
            try
            {
                Option<X509Certificate2> clientCertificate = await this.GetClientCertificate(context, actorDeviceId, actorModuleId);

                clientCredentials = clientCertificate.Match(
                cert =>
                {
                    IList<X509Certificate2> certChain = context.GetClientCertificateChain();
                    return this.identityFactory.GetWithX509Cert(actorDeviceId, actorModuleId.OrDefault(), string.Empty, cert, certChain, Option.None<string>(), authChain);
                },
                () =>
                {
                    string authHeader = this.GetAuthHeader(context);
                    return this.identityFactory.GetWithSasToken(actorDeviceId, actorModuleId.OrDefault(), string.Empty, authHeader, false, Option.None<string>(), authChain);
                });
            }
            catch (AuthenticationException ex)
            {
                Events.AuthenticationFailed($"Failed to authenticate - {ex.Message}");
                return new HttpAuthResult(false, ex.Message);
            }

            IIdentity identity = clientCredentials.Identity;

            if (!await this.authenticator.AuthenticateAsync(clientCredentials))
            {
                return new HttpAuthResult(false, Events.AuthenticationFailed($"Unable to authenticate device with Id {identity.Id}"));
            }

            Events.AuthenticationSucceeded(identity);
            return new HttpAuthResult(true, string.Empty);
        }

        string GetAuthHeader(HttpContext context)
        {
            string authHeader = context.GetAuthHeader();

            SharedAccessSignature sharedAccessSignature;
            try
            {
                sharedAccessSignature = SharedAccessSignature.Parse(this.iotHubName, authHeader);
            }
            catch (Exception ex)
            {
                throw new AuthenticationException($"Cannot parse SharedAccessSignature because of the following error - {ex.Message}", ex);
            }

            return authHeader;
        }

        async Task<Option<X509Certificate2>> GetClientCertificate(HttpContext context, string actorDeviceId, Option<string> actorModuleId)
        {
            X509Certificate2 clientCertificate = await context.Connection.GetClientCertificateAsync();

            string actorId = actorModuleId.Match(mid => $"{actorDeviceId}/{mid}", () => actorDeviceId);
            try
            {
                if (clientCertificate == null)
                {
                    return await this.httpProxiedCertificateExtractor.GetClientCertificate(context);
                }
            }
            catch (Exception e)
            {
                throw new AuthenticationException($"Unable to authenticate device with Id {actorId} - {e.Message}", e);
            }

            return Option.Maybe(clientCertificate);
        }

        static class Events
        {
            const int IdStart = HttpEventIds.HttpRequestAuthenticator;
            static readonly ILogger Log = Logger.Factory.CreateLogger<HttpRequestAuthenticator>();

            enum EventIds
            {
                AuthenticationFailed = IdStart,
                AuthenticationSuccess,
                AuthenticationApiProxy,
            }

            public static string AuthenticationFailed(string message)
            {
                Log.LogError((int)EventIds.AuthenticationFailed, $"Http Authentication failed due to following issue - {message}");
                return message;
            }

            public static void AuthenticationSucceeded(IIdentity identity)
            {
                Log.LogDebug((int)EventIds.AuthenticationSuccess, $"Http Authentication succeeded for device with Id {identity.Id}");
            }

            public static void AuthenticationApiProxy(string remoteAddress)
            {
                Log.LogDebug((int)EventIds.AuthenticationApiProxy, $"Received authentication attempt through ApiProxy for {remoteAddress}");
            }
        }
    }
}
