// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Cryptography.X509Certificates;
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

        public async Task<HttpAuthResult> AuthenticateAsync(string deviceId, Option<string> moduleId, HttpContext context)
        {
            Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            Preconditions.CheckNotNull(context, nameof(context));

            IClientCredentials clientCredentials;
            X509Certificate2 clientCertificate = await context.Connection.GetClientCertificateAsync();

            if (clientCertificate != null)
            {
                IList<X509Certificate2> certChain = context.GetClientCertificateChain();
                clientCredentials = this.identityFactory.GetWithX509Cert(deviceId, moduleId.OrDefault(), string.Empty, clientCertificate, certChain, Option.None<string>());
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

                clientCredentials = this.identityFactory.GetWithSasToken(deviceId, moduleId.OrDefault(), string.Empty, authHeader, false, Option.None<string>());
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
                AuthenticationSuccess
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
        }
    }
}
