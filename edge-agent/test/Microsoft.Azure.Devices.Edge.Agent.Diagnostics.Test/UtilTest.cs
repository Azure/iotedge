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
            Metric[] scrape1 = Enumerable.Range(1, 100).Select(i => new Metric(new DateTime(this.rand.Next(1000, 10000), DateTimeKind.Utc), $"Test Metric {i}", i, $"{i}")).ToArray();

            // all odd values are changed, so they should be removed.
            Metric[] scrape2 = scrape1.Select(m => new Metric(new DateTime(this.rand.Next(1000, 10000), DateTimeKind.Utc), m.Name, m.Value + m.Value % 2, m.Tags)).ToArray();

            Metric[] result = MetricsDeDuplication.RemoveDuplicateMetrics(scrape1.Concat(scrape2)).ToArray();
            Assert.Equal(150, result.Length);

            string[] expected = scrape1.Select(m => m.Name).Concat(scrape2.Where(m => int.Parse(m.Tags) % 2 == 1).Select(m => m.Name)).OrderBy(n => n).ToArray();
            string[] actual = result.Select(m => m.Name).OrderBy(n => n).ToArray();
            Assert.Equal(expected, actual);
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
