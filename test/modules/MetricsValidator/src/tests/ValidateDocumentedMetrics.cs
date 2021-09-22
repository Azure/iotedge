// Copyright (c) Microsoft. All rights reserved.

namespace MetricsValidator.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using MetricsValidator.Util;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Agent.Diagnostics;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    public class ValidateDocumentedMetrics : TestBase
    {
        public ValidateDocumentedMetrics(TestReporter testReporter, IMetricsScraper scraper, ModuleClient moduleClient)
            : base(testReporter, scraper, moduleClient)
        {
        }

        protected override async Task Test(CancellationToken cancellationToken)
        {
            await this.SeedMetrics(cancellationToken);
            bool hostMetricsFound = await HostMetricUtil.WaitForHostMetrics(this.scraper, cancellationToken);
            this.testReporter.Assert("Host Metrics Found", hostMetricsFound);

            // scrape metrics
            var metrics = await this.scraper.ScrapeEndpointsAsync(cancellationToken);

            var expected = this.GetExpectedMetrics();

            HashSet<string> unreturnedMetrics = new HashSet<string>(expected.Keys);
            if (expected.Count == 0)
            {
                this.testReporter.Assert("No documented metrics", false, string.Empty);
                return;
            }

            log.LogInformation("Got expected metrics");
            foreach (Metric metric in metrics)
            {
                unreturnedMetrics.Remove(metric.Name);

                string metricId = $"{metric.Name}:{JsonConvert.SerializeObject(metric.Tags)}";
                if (expected.TryGetValue(metric.Name, out string[] expectedTags))
                {
                    // Make sure all tags are returned
                    this.testReporter.Assert(metricId, expectedTags.All(metric.Tags.ContainsKey), $"Expected metric {metric.Name} to contain tags {string.Join(", ", expectedTags)}.\nActual tags: {JsonConvert.SerializeObject(metric.Tags)}");
                }
                else
                {
                    // Histogram added metrics. If histogram metric doesn't exist, it will fail earlier
                    if (!(metric.Name.EndsWith("_sum") || metric.Name.EndsWith("_count")))
                    {
                        this.testReporter.Assert(metricId, false, "Metric is not documented.");
                    }
                }
            }

            // The following metric should not be populated in a happy E2E path.
            // We are going to make a list and remove them here to not consider them as a failure.
            var skippingMetrics = new HashSet<string>
            {
                "edgeAgent_twin_signature_check_count",
                "edgeAgent_twin_signaturs_check_seconds",
                "edgeAgent_unsuccessful_iothub_syncs_total",
                "edgehub_client_connect_failed_total",
                "edgehub_messages_dropped_total",
                "edgehub_messages_unack_total",
                "edgehub_offline_count_total",
                "edgehub_operation_retry_total",
                "edgehub_twin_signature_check_count",
                "edgehub_twin_signature_check_seconds",
                "edgehub_client_disconnect_total"
            };

            if (!System.Environment.Is64BitOperatingSystem)
            {
                // This metric is not part of the scrape on ARM32 machine.
                skippingMetrics.Add("edgeAgent_created_pids_total");
            }

            foreach (string skippingMetric in skippingMetrics)
            {
                if (unreturnedMetrics.Remove(skippingMetric))
                {
                    log.LogInformation($"\"{skippingMetric}\" was depopulated");
                }
            }

            foreach (string unreturnedMetric in unreturnedMetrics)
            {
                this.testReporter.Assert(unreturnedMetric, false, $"Metric did not exist in scrape.");
            }
        }

        static readonly Regex Matcher = new Regex(@"`(\w*)`", RegexOptions.Compiled);

        Dictionary<string, string[]> GetExpectedMetrics()
        {
            try
            {
                return File.ReadAllLines(Path.Combine("doc", "BuiltInMetrics.md"))
                    .Select(line => line.Split('|'))
                    .Where(rows => rows.Length == 6)
                    .Select(line => (Matcher.Match(line[1]), Matcher.Matches(line[2])))
                    .Where(matches => matches.Item1.Success)
                    .ToDictionary(
                        matches => matches.Item1.Groups[1].Value,
                        matches => matches.Item2.Select(match => match.Groups[1].Value).ToArray());
            }
            catch (Exception ex)
            {
                this.testReporter.Assert("Read metrics docs", false, ex.Message);
                return new Dictionary<string, string[]>();
            }
        }

        async Task SeedMetrics(CancellationToken cancellationToken)
        {
            string deviceId = Environment.GetEnvironmentVariable("IOTEDGE_DEVICEID");

            await this.moduleClient.SendEventAsync(new Message(Encoding.UTF8.GetBytes("Test message to seed metrics")), cancellationToken);

            const string methodName = "FakeDirectMethod";
            await this.moduleClient.SetMethodHandlerAsync(methodName, (_, __) => Task.FromResult(new MethodResponse(200)), null);
            await this.moduleClient.InvokeMethodAsync(deviceId, Environment.GetEnvironmentVariable("IOTEDGE_MODULEID"), new MethodRequest(methodName), cancellationToken);

            await this.moduleClient.UpdateReportedPropertiesAsync(new TwinCollection(), cancellationToken);
            await this.moduleClient.GetTwinAsync(cancellationToken);

            await Task.Delay(TimeSpan.FromSeconds(10));
        }
    }
}
