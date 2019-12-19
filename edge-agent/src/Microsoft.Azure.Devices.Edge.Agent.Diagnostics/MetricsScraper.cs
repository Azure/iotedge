// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Diagnostics
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Diagnostics.Util;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public sealed class MetricsScraper : IMetricsScraper, IDisposable
    {
        const string UrlPattern = @"[^/:]+://(?<host>[^/:]+)(:[^:]+)?$";
        static readonly Regex UrlRegex = new Regex(UrlPattern, RegexOptions.Compiled);
        readonly HttpClient httpClient;
        readonly Lazy<IDictionary<string, string>> endpoints;
        readonly ISystemTime systemTime;
        static readonly ILogger Log = Logger.Factory.CreateLogger<MetricsScraper>();

        /// <summary>
        /// Initializes a new instance of the <see cref="MetricsScraper"/> class.
        /// </summary>
        /// <param name="endpoints">List of endpoints to scrape from. Endpoints must expose metrics in the prometheous format.
        /// Endpoints should be in the form "http://edgeHub:9600/metrics".</param>
        /// <param name="systemTime">Source for current time.</param>
        public MetricsScraper(IList<string> endpoints, ISystemTime systemTime = null)
        {
            Preconditions.CheckNotNull(endpoints, nameof(endpoints));

            this.httpClient = new HttpClient();
            this.endpoints = new Lazy<IDictionary<string, string>>(() => endpoints.ToDictionary(e => e, this.GetUriWithIpAddress));
            this.systemTime = systemTime ?? SystemTime.Instance;
        }

        public Task<IEnumerable<Metric>> ScrapeEndpointsAsync(CancellationToken cancellationToken)
        {
            return this.endpoints.Value.SelectManyAsync(async endpoint =>
            {
                Log.LogInformation($"Scraping endpoint {endpoint.Key}");
                string metricsData = await this.ScrapeEndpoint(endpoint.Value, cancellationToken);
                return PrometheusMetricsParser.ParseMetrics(this.systemTime.UtcNow, metricsData);
            });
        }

        public void Dispose()
        {
            this.httpClient.Dispose();
        }

        string GetUriWithIpAddress(string endpoint)
        {
            Log.LogInformation($"Getting uri with Ip for {endpoint}");
            Match match = UrlRegex.Match(endpoint);
            if (!match.Success)
            {
                throw new InvalidOperationException($"Endpoint {endpoint} is not a valid URL in the format <protocol>://<host>:<port>/<parameters>");
            }

            var hostGroup = match.Groups["host"];
            string host = hostGroup.Value;
            var ipHostEntry = Dns.GetHostEntry(host);
            var ipAddr = ipHostEntry.AddressList.Length > 0 ? ipHostEntry.AddressList[0].ToString() : string.Empty;
            var builder = new UriBuilder(endpoint);
            builder.Host = ipAddr;
            string endpointWithIp = builder.Uri.ToString();
            Log.LogInformation($"Endpoint = {endpoint}, IP Addr = {ipAddr}, Endpoint with Ip = {endpointWithIp}");
            return endpointWithIp;
        }

        async Task<string> ScrapeEndpoint(string endpoint, CancellationToken cancellationToken)
        {
            try
            {
                HttpResponseMessage result = await this.httpClient.GetAsync(endpoint, cancellationToken);
                if (result.IsSuccessStatusCode)
                {
                    return await result.Content.ReadAsStringAsync();
                }
                else
                {
                    Log.LogInformation($"Error connecting to {endpoint} with result error code {result.StatusCode}");
                }
            }
            catch (Exception e)
            {
                Log.LogInformation($"Error scraping endpoint {endpoint} - {e}");
            }

            return string.Empty;
        }
    }
}
