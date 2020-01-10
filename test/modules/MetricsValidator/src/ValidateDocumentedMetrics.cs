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
    using Newtonsoft.Json;

    public class ValidateDocumentedMetrics
    {
        TestReporter testReporter;
        IMetricsScraper scraper;

        public ValidateDocumentedMetrics(TestReporter testReporter, IMetricsScraper scraper)
        {
            this.testReporter = testReporter.MakeSubcategory(nameof(ValidateDocumentedMetrics));
            this.scraper = scraper;
        }

        public async Task Start(CancellationToken cancellationToken)
        {
            // scrape metrics
            var metrics = await this.scraper.ScrapeEndpointsAsync(cancellationToken);

            // read documented metrics
            var lines = File.ReadAllLines(@"C:\Users\Lee\source\repos\iotedge\doc\EdgeAgentMetrics.md").Skip(2);
            Dictionary<string, string[]> expected = lines.Select(line => line.Split('|')).Where(rows => rows.Length == 4).ToDictionary(rows => rows[0].Trim(), rows => rows[1].Split(',').Select(tag => tag.Trim()).ToArray());
            expected.Remove(string.Empty);

            // track returned metrics
            HashSet<string> unreturnedMetrics = new HashSet<string>(expected.Keys);

            foreach (Metric metric in metrics)
            {
                unreturnedMetrics.Remove(metric.Name);

                if (expected.TryGetValue(metric.Name, out string[] expectedTags))
                {
                    // Make sure all tags are returned
                    this.testReporter.Assert($"{metric.Name}:{JsonConvert.SerializeObject(metric.Tags)}", expectedTags.All(metric.Tags.ContainsKey), $"Expected metric {metric.Name} to contain tags {expectedTags}.\nActual tags: {metric.Tags}");
                }
                else
                {
                    // warning, metric not documented
                }
            }

            this.testReporter.Assert("All metrics returned", !unreturnedMetrics.Any(), $"Expected metrics missing: {string.Join(", ", unreturnedMetrics)}");
        }
    }
}
