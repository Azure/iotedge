// Copyright (c) Microsoft. All rights reserved.

namespace MetricsValidator
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Diagnostics;
    using Microsoft.Azure.Devices.Edge.Util.Metrics;
    using Newtonsoft.Json;

    public class ValidateDocumentedMetrics
    {
        TestReporter testReporter;
        IMetricsScraper scraper;
        MetricFilter metricFilter;

        public ValidateDocumentedMetrics(TestReporter testReporter, IMetricsScraper scraper)
        {
            this.testReporter = testReporter.MakeSubcategory(nameof(ValidateDocumentedMetrics));
            this.scraper = scraper;
            this.metricFilter = new MetricFilter()
                .AddTagsToRemove(MetricsConstants.MsTelemetry, MetricsConstants.IotHubLabel, MetricsConstants.DeviceIdLabel);
        }

        public async Task Start(CancellationToken cancellationToken)
        {
            Console.WriteLine($"Starting {nameof(ValidateDocumentedMetrics)}");

            // scrape metrics
            var metrics = await this.scraper.ScrapeEndpointsAsync(cancellationToken);
            metrics = this.metricFilter.FilterMetrics(metrics);

            // read documented metrics
            IEnumerable<string> lines = Enumerable.Empty<string>();
            try
            {
                lines = File.ReadAllLines(Path.Combine("doc", "EdgeAgentMetrics.md")).Skip(2);
            }
            catch (Exception ex)
            {
                this.testReporter.Assert("Read metrics docs", false, ex.Message);
            }

            Dictionary<string, string[]> expected = lines.Select(line => line.Split('|')).Where(rows => rows.Length == 4).ToDictionary(rows => rows[0].Trim(), rows => rows[1].Split(',').Select(tag => tag.Trim()).ToArray());
            expected.Remove(string.Empty);

            // track returned metrics
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
    }
}
