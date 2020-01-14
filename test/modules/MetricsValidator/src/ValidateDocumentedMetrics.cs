// Copyright (c) Microsoft. All rights reserved.

namespace MetricsValidator
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Agent.Diagnostics;
    using Microsoft.Azure.Devices.Edge.Util.Metrics;
    using Newtonsoft.Json;

    public class ValidateDocumentedMetrics : TestBase
    {
        MetricFilter metricFilter;

        public ValidateDocumentedMetrics(TestReporter testReporter, IMetricsScraper scraper, ModuleClient moduleClient)
            : base(testReporter, scraper, moduleClient)
        {
            this.metricFilter = new MetricFilter()
                .AddTagsToRemove(MetricsConstants.MsTelemetry, MetricsConstants.IotHubLabel, MetricsConstants.DeviceIdLabel);
        }

        public override async Task Start(CancellationToken cancellationToken)
        {
            Console.WriteLine($"Starting {nameof(ValidateDocumentedMetrics)}");

            await this.moduleClient.SendEventAsync(new Message(Encoding.UTF8.GetBytes("Test message to seed metrics")));

            // scrape metrics
            var metrics = await this.scraper.ScrapeEndpointsAsync(cancellationToken);
            metrics = this.metricFilter.FilterMetrics(metrics);

            var expected = this.GetExpectedMetrics();
            HashSet<string> unreturnedMetrics = new HashSet<string>(expected.Keys);

            Console.WriteLine("Got expected metrics");
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
                    // Console.WriteLine($"{metricId} not documented");
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
                return File.ReadAllLines(Path.Combine("doc", "EdgeAgentMetrics.md"))
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
    }
}
