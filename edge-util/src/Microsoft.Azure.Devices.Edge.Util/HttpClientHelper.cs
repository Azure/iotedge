// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util.Uds;
    using Microsoft.Extensions.Logging;

    public class HttpClientHelper
    {
        const string HttpScheme = "http";
        const string HttpsScheme = "https";
        const string UnixScheme = "unix";

        public static HttpClient GetHttpClient(Uri serverUri)
        {
            HttpClient client;

            if (serverUri.Scheme.Equals(HttpScheme, StringComparison.OrdinalIgnoreCase) || serverUri.Scheme.Equals(HttpsScheme, StringComparison.OrdinalIgnoreCase))
            {
                client = new HttpClient(new LoggingHandler(new HttpClientHandler()));
                return client;
            }

            if (serverUri.Scheme.Equals(UnixScheme, StringComparison.OrdinalIgnoreCase))
            {
                client = new HttpClient(new LoggingHandler(new HttpUdsMessageHandler(serverUri)));
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

    class LoggingHandler : DelegatingHandler
    {
        public static readonly ILogger Log = Logger.Factory.CreateLogger<LoggingHandler>();

        public LoggingHandler(HttpMessageHandler innerHandler)
            : base(innerHandler)
        {
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var requestContent = string.Empty;
            if (request.Content != null)
            {
                requestContent = await request.Content.ReadAsStringAsync();
            }

            Log.LogInformation($"Request:\n\t{request}\n\t{requestContent}\n");

            HttpResponseMessage response = await base.SendAsync(request, cancellationToken);

            var responseContent = string.Empty;
            if (response.Content != null)
            {
                responseContent = await response.Content.ReadAsStringAsync();
            }

            Log.LogInformation($"Response:\n\t{response}\n\t{responseContent}");

            return response;
        }
    }
}
