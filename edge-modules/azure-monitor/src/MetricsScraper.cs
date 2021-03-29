// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace Microsoft.Azure.Devices.Edge.Azure.Monitor
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    public sealed class MetricsScraper : IDisposable
    {
        const string UrlPattern = @"[^/:]+://(?<host>[^/:]+)(:[^:]+)?$";
        static readonly Regex UrlRegex = new Regex(UrlPattern, RegexOptions.Compiled);
        readonly HttpClient httpClient;
        readonly IList<string> endpoints;
        readonly ISystemTime systemTime;

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
            this.endpoints = endpoints;
            this.systemTime = systemTime ?? SystemTime.Instance;
        }

        /// <summary>
        /// Scrapes metrics from all endpoints
        /// </summary>
        public Task<IEnumerable<Metric>> ScrapeEndpointsAsync(CancellationToken cancellationToken)
        {
            return SelectManyAsync(this.endpoints, async endpoint =>
            {
                Logger.Writer.LogInformation($"Scraping endpoint {endpoint}");
                string metricsData = await this.ScrapeEndpoint(endpoint, cancellationToken);
                IEnumerable<Metric> parsedMetrics = PrometheusMetricsParser.ParseMetrics(this.systemTime.UtcNow, metricsData, endpoint: endpoint);
                Logger.Writer.LogInformation($"Scraping finished, received {parsedMetrics.Count()} metrics from endpoint {endpoint}");
                return parsedMetrics;
            });
        }

        /// <summary>
        /// Taken from LinqEx.cs in Microsoft.Azure.Devices.Edge.Util and modified to not be an extension method. It does not appear to have a specific unit test.
        /// </summary>
        public async Task<IEnumerable<T1>> SelectManyAsync<T, T1>(IEnumerable<T> source, Func<T, Task<IEnumerable<T1>>> selector)
        {
            return (await Task.WhenAll(source.Select(selector))).SelectMany(s => s);
        }

        public void Dispose()
        {
            this.httpClient.Dispose();
        }

        string GetUriWithIpAddress(string endpoint)
        {
            Logger.Writer.LogDebug($"Getting uri with Ip for {endpoint}");
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
            Logger.Writer.LogDebug($"Endpoint = {endpoint}, IP Addr = {ipAddr}, Endpoint with Ip = {endpointWithIp}");
            return endpointWithIp;
        }

        async Task<string> ScrapeEndpoint(string endpoint, CancellationToken cancellationToken)
        {
            try
            {
                // Temporary. Only needed until edgeHub starts using asp.net to expose endpoints
                endpoint = this.GetUriWithIpAddress(endpoint);

                HttpResponseMessage result = await this.httpClient.GetAsync(endpoint, cancellationToken);
                if (result.IsSuccessStatusCode)
                {
                    return await result.Content.ReadAsStringAsync();
                }
                else
                {
                    Logger.Writer.LogError($"Error connecting to {endpoint} with result error code {result.StatusCode}");
                }
            }
            catch (System.Net.Sockets.SocketException e) when (e.Source == "System.Net.NameResolution")
            {
                Logger.Writer.LogError($"Error scraping endpoint {endpoint}, hostname likely can not be found - {e}");
            }
            catch (Exception e)
            {
                Logger.Writer.LogError($"Error scraping endpoint {endpoint} - {e}");
            }

            return string.Empty;
        }
    }
}
