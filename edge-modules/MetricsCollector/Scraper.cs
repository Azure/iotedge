// Copyright (c) Microsoft. All rights reserved.
namespace MetricsCollector
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class Scraper
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger("MetricsCollector");
        const string UrlPattern = @"[^/:]+://(?<host>[^/:]+)(:[^:]+)?$";
        static readonly Regex UrlRegex = new Regex(UrlPattern, RegexOptions.Compiled);
        readonly HttpClient httpClient;
        readonly IDictionary<string, string> endpoints = new Dictionary<string, string>();

        public Scraper(IList<string> endpoints)
        {
            this.httpClient = new HttpClient();
            this.endpoints = endpoints.ToDictionary(e => e, GetUriWithIpAddress);
        }

        static string GetUriWithIpAddress(string endpoint)
        {
            Logger.LogInformation($"Getting uri with Ip for {endpoint}");
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
            Logger.LogInformation($"Endpoint = {endpoint}, IP Addr = {ipAddr}, Endpoint with Ip = {endpointWithIp}");
            return endpointWithIp;
        }

        public async Task<IEnumerable<string>> Scrape()
        {
            var metrics = new List<string>();
            foreach (KeyValuePair<string, string> endpoint in this.endpoints)
            {
                Logger.LogInformation($"Scraping endpoint {endpoint.Key}");
                string metricsData = await this.ScrapeEndpoint(endpoint.Value);
                Logger.LogInformation($"Got metrics from endpoint {endpoint}");
                metrics.Add(metricsData);
            }

            return metrics;
        }

        async Task<string> ScrapeEndpoint(string endpoint)
        {
            try
            {
                var result = await this.httpClient.GetAsync(endpoint);
                if (result.IsSuccessStatusCode)
                {
                    return await result.Content.ReadAsStringAsync();
                }
                else
                {
                    Logger.LogError($"Result error code {result.StatusCode}");
                    throw new InvalidOperationException("Error connecting EdgeHub");
                }
            }
            catch (Exception e)
            {
                Logger.LogError($"Error scraping endpoint {endpoint} - {e.Message}");
                throw;
            }
        }
    }
}
