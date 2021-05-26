// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using System.Linq;
    using System.Net.Http;
    using Microsoft.Azure.Devices.Edge.Util.Uds;

    public class HttpClientHelper
    {
        const string HttpScheme = "http";
        const string HttpsScheme = "https";
        const string UnixScheme = "unix";

        static Lazy<HttpClient> client = new Lazy<HttpClient>(() => new HttpClient(), isThreadSafe: true);

        public static HttpClient GetHttpClient(Uri serverUri)
        {
            HttpClient client;

            if (serverUri.Scheme.Equals(HttpScheme, StringComparison.OrdinalIgnoreCase) || serverUri.Scheme.Equals(HttpsScheme, StringComparison.OrdinalIgnoreCase))
            {
                return HttpClientHelper.client.Value;
            }

            if (serverUri.Scheme.Equals(UnixScheme, StringComparison.OrdinalIgnoreCase))
            {
                client = new HttpClient(new HttpUdsMessageHandler(serverUri));
                return client;
            }

            throw new InvalidOperationException("ProviderUri scheme is not supported");
        }

        public static string GetBaseUrl(Uri serverUri)
        {
            if (serverUri.Scheme.Equals(UnixScheme, StringComparison.OrdinalIgnoreCase))
            {
                return $"{HttpScheme}://{serverUri.Segments.Last()}";
            }

            return serverUri.OriginalString;
        }
    }
}
