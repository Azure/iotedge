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

        public static HttpClient GetHttpClient(Uri serverUri)
        {
            return GetHttpClient(serverUri, Option.None<TimeSpan>());
        }

        public static HttpClient GetHttpClient(Uri serverUri, TimeSpan timeout)
        {
            return GetHttpClient(serverUri, Option.Some(timeout));
        }

        static HttpClient GetHttpClient(Uri serverUri, Option<TimeSpan> timeout)
        {
            HttpClient client;

            if (serverUri.Scheme.Equals(HttpScheme, StringComparison.OrdinalIgnoreCase) || serverUri.Scheme.Equals(HttpsScheme, StringComparison.OrdinalIgnoreCase))
            {
                client = new HttpClient();
            }
            else if (serverUri.Scheme.Equals(UnixScheme, StringComparison.OrdinalIgnoreCase))
            {
                client = new HttpClient(new HttpUdsMessageHandler(serverUri));
            }
            else
            {
                throw new InvalidOperationException("ProviderUri scheme is not supported");
            }

            timeout.Map(t => client.Timeout = t);
            return client;
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
