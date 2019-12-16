// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Diagnostics.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Diagnostics.Util;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using Newtonsoft.Json;
    using Xunit;

    public class UtilTest
    {
        Random rand = new Random();

        [Fact]
        public void TestRemoveDuplicateMetrics()
        {
            /* fake data */
            DateTime baseTime = new DateTime(10000000, DateTimeKind.Utc);
            int start = 10;
            int n = 100; // Keep this value even

            // baseline
            Metric[] scrape1 = Enumerable.Range(start, n).Select(i => new Metric(baseTime, $"Test Metric {i}", i, $"Tags")).ToArray();

            // second half is changed
            Metric[] scrape2_1 = Enumerable.Range(start, n / 2).Select(i => new Metric(baseTime, $"Test Metric {i}", i, $"Tags")).ToArray();
            Metric[] scrape2_2 = Enumerable.Range(start + n / 2, n / 2).Select(i => new Metric(baseTime, $"Test Metric {i}", i * 2, $"Tags")).ToArray();
            Metric[] scrape2 = scrape2_1.Concat(scrape2_2).ToArray();

            // everything changed
            Metric[] scrape3 = Enumerable.Range(start, n).Select(i => new Metric(baseTime, $"Test Metric {i}", i / 2.0, $"Tags")).ToArray();

            /* test */
            IEnumerable<Metric> test1 = scrape1.Concat(scrape1).Concat(scrape1).Concat(scrape1);
            IEnumerable<Metric> result1 = MetricsDeDuplication.RemoveDuplicateMetrics(test1);
            Assert.Equal(n * 2, result1.Count()); // Keeps the first and last results

            IEnumerable<Metric> test2 = scrape1.Concat(scrape1).Concat(scrape1).Concat(scrape2).Concat(scrape2);
            IEnumerable<Metric> result2 = MetricsDeDuplication.RemoveDuplicateMetrics(test2);
            Assert.Equal(n * 2 + n, result2.Count()); // Keeps the first and last results of the baseline, and the first and last results of the second half of the second scrape

            IEnumerable<Metric> test3 = scrape1.Concat(scrape1).Concat(scrape1).Concat(scrape1).Concat(scrape3);
            IEnumerable<Metric> result3 = MetricsDeDuplication.RemoveDuplicateMetrics(test3);
            Assert.Equal(n * 2 + n, result3.Count()); // Keeps the first and last results of the baseline, and the results of the of the third scrape

            IEnumerable<Metric> test4 = scrape1.Concat(scrape3);
            IEnumerable<Metric> result4 = MetricsDeDuplication.RemoveDuplicateMetrics(test4);
            Assert.Equal(n + n, result4.Count()); // Keeps the baseline, and the results of the of the third scrape

            IEnumerable<Metric> test5 = scrape1.Concat(scrape2).Concat(scrape2).Concat(scrape2).Concat(scrape3);
            IEnumerable<Metric> result5 = MetricsDeDuplication.RemoveDuplicateMetrics(test5);
            int fromScrape1 = n; // one result from first scrape.
            int fromScrape2 = n / 2 + n; // initial change catches half, final change gets all
            int fromScrape3 = n; // Changes all
            Assert.Equal(fromScrape1 + fromScrape2 + fromScrape3, result5.Count());
        }

        [Fact]
        public void TestRemoveDuplicateKeepsLine()
        {
            DateTime baseTime = new DateTime(10000000, DateTimeKind.Utc);

            Metric[] testMetrics = new Metric[]
            {
                new Metric(baseTime, "Test", 1, "Tags"),
                new Metric(baseTime.AddMinutes(1), "Test", 1, "Tags"),
                new Metric(baseTime.AddMinutes(2), "Test", 1, "Tags"),
                new Metric(baseTime.AddMinutes(3), "Test", 1, "Tags"),
                new Metric(baseTime.AddMinutes(4), "Test", 2, "Tags"),
                new Metric(baseTime.AddMinutes(5), "Test", 3, "Tags"),
                new Metric(baseTime.AddMinutes(6), "Test", 3, "Tags"),
                new Metric(baseTime.AddMinutes(7), "Test", 3, "Tags"),
                new Metric(baseTime.AddMinutes(8), "Test", 3, "Tags"),
                new Metric(baseTime.AddMinutes(9), "Test", 3, "Tags"),
            };

            Metric[] expected = new Metric[]
            {
                new Metric(baseTime, "Test", 1, "Tags"),
                new Metric(baseTime.AddMinutes(3), "Test", 1, "Tags"),
                new Metric(baseTime.AddMinutes(4), "Test", 2, "Tags"),
                new Metric(baseTime.AddMinutes(5), "Test", 3, "Tags"),
                new Metric(baseTime.AddMinutes(9), "Test", 3, "Tags"),
            };

            Metric[] result = MetricsDeDuplication.RemoveDuplicateMetrics(testMetrics).ToArray();
            Assert.Equal(expected, result);
        }

        [Fact]
        public void TestParsesBasicMetrics()
        {
            int n = 10;

            /* test data */
            (string name, string module, double value)[] fakeScrape = Enumerable.Range(1, n).SelectMany(i => Enumerable.Range(100, n).Select(j => ($"metric_{i}", $"module_{j}", (double)i * j))).ToArray();
            DateTime dateTime = DateTime.UtcNow;

            string temp = this.GenerateFakeScrape(fakeScrape);
            Metric[] parsedMetrics = PrometheusMetricsParser.ParseMetrics(dateTime, this.GenerateFakeScrape(fakeScrape)).ToArray();

            for (int i = 0; i < fakeScrape.Length; i++)
            {
                Assert.Equal(fakeScrape[i].name, parsedMetrics[i].Name);
                Assert.Equal(fakeScrape[i].value, parsedMetrics[i].Value);
                Assert.Equal(dateTime, parsedMetrics[i].TimeGeneratedUtc);

                var tags = JsonConvert.DeserializeObject<Dictionary<string, string>>(parsedMetrics[i].Tags);
                Assert.Equal("fakeHub", tags["iothub"]);
                Assert.Equal("fakeDevice", tags["edge_device"]);
                Assert.Equal("1", tags["instance_number"]);
                Assert.Equal(fakeScrape[i].module, tags["module_name"]);
            }
        }

        private string GenerateFakeScrape(IEnumerable<(string name, string module, double value)> data)
        {
            string dataPoints = string.Join("\n", data.Select(d => $@"
{d.name}{{iothub=""fakeHub"",edge_device=""fakeDevice"",instance_number=""1"",module_name=""{d.module}""}} {d.value}
"));
            string metricsString = $@"
# HELP edgeagent_module_start_total Start command sent to module
# TYPE edgeagent_module_start_total counter
{dataPoints}
";

            return metricsString;
        }
    }
}
