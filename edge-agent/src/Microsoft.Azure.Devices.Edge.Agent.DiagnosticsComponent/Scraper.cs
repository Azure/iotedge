// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.MetricsCollector
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

    public interface IScraper
    {
        Task<IDictionary<string, string>> ScrapeAsync(CancellationToken cancellationToken);
    }

    public class Scraper : IScraper, IDisposable
    {
        const string UrlPattern = @"[^/:]+://(?<host>[^/:]+)(:[^:]+)?$";
        static readonly Regex UrlRegex = new Regex(UrlPattern, RegexOptions.Compiled);
        readonly HttpClient httpClient;
        readonly Lazy<IDictionary<string, string>> endpoints;

        public Scraper(IList<string> endpoints)
        {
            this.httpClient = new HttpClient();
            this.endpoints = new Lazy<IDictionary<string, string>>(() => endpoints.ToDictionary(e => e, GetUriWithIpAddress));
        }

        static string GetUriWithIpAddress(string endpoint)
        {
            Console.WriteLine($"Getting uri with Ip for {endpoint}");
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
            Console.WriteLine($"Endpoint = {endpoint}, IP Addr = {ipAddr}, Endpoint with Ip = {endpointWithIp}");
            return endpointWithIp;
        }

        public void Dispose()
        {
            this.httpClient.Dispose();
        }

        public async Task<IDictionary<string, string>> ScrapeAsync(CancellationToken cancellationToken)
        {
            var metrics = new Dictionary<string, string>();
            foreach (KeyValuePair<string, string> endpoint in this.endpoints.Value)
            {
                Console.WriteLine($"Scraping endpoint {endpoint.Key}");
                string metricsData = await this.ScrapeEndpoint(endpoint.Value, cancellationToken);
                metrics.Add(endpoint.Key, metricsData);
            }

            return metrics;
        }

        private async Task<string> ScrapeEndpoint(string endpoint, CancellationToken cancellationToken)
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
                    Console.WriteLine($"Result error code {result.StatusCode}");
                    throw new InvalidOperationException("Error connecting EdgeHub");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error scraping endpoint {endpoint} - {e.Message}");
                throw;
            }
        }
    }
}
