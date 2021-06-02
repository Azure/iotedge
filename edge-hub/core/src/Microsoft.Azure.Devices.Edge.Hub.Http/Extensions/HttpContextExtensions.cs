// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http.Extensions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Security.Authentication;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Azure.Devices.Common.Security;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Extensions.Primitives;
    using Microsoft.Net.Http.Headers;

    public static class HttpContextExtensions
    {
        public static IList<X509Certificate2> GetClientCertificateChain(this HttpContext context)
        {
            var feature = context.Features.Get<ITlsConnectionFeatureExtended>();
            return (feature == null) ? new List<X509Certificate2>() : feature.ChainElements;
        }

        public static X509Certificate2 GetForwardedCertificate(this HttpContext context)
        {
            X509Certificate2 clientCertificate = null;

            if (context.Request.Headers.TryGetValue(Constants.ClientCertificateHeaderKey, out StringValues clientCertHeader) && clientCertHeader.Count > 0)
            {
                string clientCertString = WebUtility.UrlDecode(clientCertHeader.First());

                var clientCertificateBytes = Encoding.UTF8.GetBytes(clientCertString);
                clientCertificate = new X509Certificate2(clientCertificateBytes);
            }

            return clientCertificate;
        }

        public static string GetAuthHeader(this HttpContext context)
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
                throw new AuthenticationException("Authorization header missing");
            }
            else if (authorizationQueryParameters.Count != 1 && authorizationHeaderValues.Count != 1)
            {
                throw new AuthenticationException("Invalid authorization header count");
            }

            string authHeader = authorizationQueryParameters.Count == 1
                ? authorizationQueryParameters.First()
                : authorizationHeaderValues.First();

            if (!authHeader.StartsWith("SharedAccessSignature", StringComparison.OrdinalIgnoreCase))
            {
                throw new AuthenticationException("Invalid Authorization header. Only SharedAccessSignature is supported.");
            }

            return authHeader;
        }
    }
}
