// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Azure.Devices.Common.Security;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Hub.Http.Extensions;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Primitives;
    using Microsoft.Net.Http.Headers;

    public class HttpRequestAuthenticator : IHttpRequestAuthenticator
    {
        readonly IAuthenticator authenticator;
        readonly IClientCredentialsFactory identityFactory;
        readonly string iotHubName;

        public HttpRequestAuthenticator(
            IAuthenticator authenticator,
            IClientCredentialsFactory identityFactory,
            string iotHubName)
        {
            this.authenticator = Preconditions.CheckNotNull(authenticator, nameof(authenticator));
            this.identityFactory = Preconditions.CheckNotNull(identityFactory, nameof(identityFactory));
            this.iotHubName = Preconditions.CheckNonWhiteSpace(iotHubName, nameof(iotHubName));
        }

        public async Task<HttpAuthResult> AuthenticateAsync(string actorDeviceId, Option<string> actorModuleId, Option<string> authChain, HttpContext context)
        {
            Preconditions.CheckNonWhiteSpace(actorDeviceId, nameof(actorDeviceId));
            Preconditions.CheckNotNull(context, nameof(context));

            IClientCredentials clientCredentials;
            X509Certificate2 clientCertificate = await context.Connection.GetClientCertificateAsync();

            if (clientCertificate == null)
            {
                // If the connection came through the API proxy, the client cert
                // would have been forwarded in a custom header. But since TLS
                // termination occurs at the proxy, we can only trust this custom
                // header if the request came through port 8080, which an internal
                // port only accessible within the local Docker vNet.
                if (context.Connection.LocalPort == Constants.ApiProxyPort)
                {
                    if (context.Request.Headers.TryGetValue(Constants.ClientCertificateHeaderKey, out StringValues clientCertHeader) && clientCertHeader.Count > 0)
                    {
                        Events.AuthenticationApiProxy(context.Connection.RemoteIpAddress.ToString());

                        string clientCertString = WebUtility.UrlDecode(clientCertHeader.First());

                        try
                        {
                            var clientCertificateBytes = Encoding.UTF8.GetBytes(clientCertString);
                            clientCertificate = new X509Certificate2(clientCertificateBytes);
                        }
                        catch (Exception ex)
                        {
                            return new HttpAuthResult(false, Events.AuthenticationFailed($"Invalid client certificate: {ex.Message}"));
                        }
                    }
                }
            }

            if (clientCertificate != null)
            {
                IList<X509Certificate2> certChain = context.GetClientCertificateChain();
                clientCredentials = this.identityFactory.GetWithX509Cert(actorDeviceId, actorModuleId.OrDefault(), string.Empty, clientCertificate, certChain, Option.None<string>(), authChain);
            }
            else
            {
                // Authorization header may be present in the QueryNameValuePairs as per Azure standards,
                // So check in the query parameters first.
                List<string> authorizationQueryParameters = context.Request.Query
                    .Where(p => p.Key.Equals(HeaderNames.Authorization, StringComparison.OrdinalIgnoreCase))
                    .SelectMany(p => p.Value)
                    .ToList();

                if (!(context.Request.Headers.TryGetValue(HeaderNames.Authorization, out StringValues authorizationHeaderValues)
                      && authorizationQueryParameters.Count == 0))
                {
                    return new HttpAuthResult(false, Events.AuthenticationFailed("Authorization header missing"));
                }
                else if (authorizationQueryParameters.Count != 1 && authorizationHeaderValues.Count != 1)
                {
                    return new HttpAuthResult(false, Events.AuthenticationFailed("Invalid authorization header count"));
                }

                string authHeader = authorizationQueryParameters.Count == 1
                    ? authorizationQueryParameters.First()
                    : authorizationHeaderValues.First();

                if (!authHeader.StartsWith("SharedAccessSignature", StringComparison.OrdinalIgnoreCase))
                {
                    return new HttpAuthResult(false, Events.AuthenticationFailed("Invalid Authorization header. Only SharedAccessSignature is supported."));
                }

                try
                {
                    SharedAccessSignature sharedAccessSignature = SharedAccessSignature.Parse(this.iotHubName, authHeader);
                    if (sharedAccessSignature.IsExpired())
                    {
                        return new HttpAuthResult(false, Events.AuthenticationFailed("SharedAccessSignature is expired"));
                    }
                }
                catch (Exception ex)
                {
                    return new HttpAuthResult(false, Events.AuthenticationFailed($"Cannot parse SharedAccessSignature because of the following error - {ex.Message}"));
                }

                clientCredentials = this.identityFactory.GetWithSasToken(actorDeviceId, actorModuleId.OrDefault(), string.Empty, authHeader, false, Option.None<string>(), authChain);
            }

            IIdentity identity = clientCredentials.Identity;

            if (!await this.authenticator.AuthenticateAsync(clientCredentials))
            {
                return new HttpAuthResult(false, Events.AuthenticationFailed($"Unable to authenticate device with Id {identity.Id}"));
            }

            Events.AuthenticationSucceeded(identity);
            return new HttpAuthResult(true, string.Empty);
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
                Log.LogWarning((int)EventIds.AuthenticationFailed, $"Http Authentication failed due to following issue - {message}");
                return message;
            }

            public static void AuthenticationSucceeded(IIdentity identity)
            {
                Log.LogDebug((int)EventIds.AuthenticationSuccess, $"Http Authentication succeeded for device with Id {identity.Id}");
            }

            public static void AuthenticationApiProxy(string remoteAddress)
            {
                Log.LogInformation((int)EventIds.AuthenticationApiProxy, $"Received authentication attempt through ApiProxy for {remoteAddress}");
            }
        }
    }
}
