// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.DiagnosticsComponent
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
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class MetricsScraper : IMetricsScraper, IDisposable
    {
        const string UrlPattern = @"[^/:]+://(?<host>[^/:]+)(:[^:]+)?$";
        static readonly Regex UrlRegex = new Regex(UrlPattern, RegexOptions.Compiled);
        readonly HttpClient httpClient;
        readonly Lazy<IDictionary<string, string>> endpoints;
        static readonly ILogger Log = Logger.Factory.CreateLogger<MetricsScraper>();

        public MetricsScraper(IList<string> endpoints)
        {
            this.httpClient = new HttpClient();
            this.endpoints = new Lazy<IDictionary<string, string>>(() => endpoints.ToDictionary(e => e, this.GetUriWithIpAddress));
        }

        public async Task<IDictionary<string, string>> ScrapeAsync(CancellationToken cancellationToken)
        {
            var metrics = new Dictionary<string, string>();
            foreach (KeyValuePair<string, string> endpoint in this.endpoints.Value)
            {
                Log.LogInformation($"Scraping endpoint {endpoint.Key}");
                string metricsData = await this.ScrapeEndpoint(endpoint.Value, cancellationToken);
                metrics.Add(endpoint.Key, metricsData);
            }

            return metrics;
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
                var result = await this.httpClient.GetAsync(endpoint, cancellationToken);
                if (result.IsSuccessStatusCode)
                {
                    return await result.Content.ReadAsStringAsync();
                }
                else
                {
                    Log.LogInformation($"Error connecting to {endpoint}\nResult error code {result.StatusCode}");
                }
            }
            catch (Exception e)
            {
                Log.LogInformation($"Error scraping endpoint {endpoint} - {e.Message}");
            }

            return string.Empty;
        }
    }
}
