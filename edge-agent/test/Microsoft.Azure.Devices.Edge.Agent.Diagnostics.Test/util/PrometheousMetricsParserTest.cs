// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Diagnostics.Test.Util
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Agent.Diagnostics.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Unit]
    public class PrometheousMetricsParserTest
    {
        [Fact]
        public void TestParsesBasicMetrics()
        {
            int n = 10;

            /* test data */
            (string name, string module, double value)[] fakeScrape = Enumerable.Range(1, n).SelectMany(i => Enumerable.Range(100, n).Select(j => ($"metric_{i}", $"module_{j}", (double)i * j))).ToArray();
            DateTime dateTime = DateTime.UtcNow;

            Metric[] parsedMetrics = PrometheusMetricsParser.ParseMetrics(dateTime, this.GenerateFakeScrape(fakeScrape)).ToArray();
            Assert.NotEmpty(parsedMetrics);

            for (int i = 0; i < fakeScrape.Length; i++)
            {
                Assert.Equal(fakeScrape[i].name, parsedMetrics[i].Name);
                Assert.Equal(fakeScrape[i].value, parsedMetrics[i].Value);
                Assert.Equal(dateTime, parsedMetrics[i].TimeGeneratedUtc);

                Assert.Equal("fakeHub", parsedMetrics[i].Tags["iothub"]);
                Assert.Equal("fakeDevice", parsedMetrics[i].Tags["edge_device"]);
                Assert.Equal(string.Empty, parsedMetrics[i].Tags["blank_tag"]);
                Assert.Equal("1", parsedMetrics[i].Tags["instance_number"]);
                Assert.Equal(fakeScrape[i].module, parsedMetrics[i].Tags["module_name"]);
            }
        }

        [Fact]
        public void TestParsesNumberValuesOnly()
        {
            DateTime dateTime = DateTime.UtcNow;

            string temp = this.GenerateFakeScrape(new[] { ("test", "testm", 5.0) });
            Metric[] parsedMetrics = PrometheusMetricsParser.ParseMetrics(dateTime, temp).ToArray();
            Assert.Single(parsedMetrics);

            temp = this.GenerateFakeScrape(new[] { ("test", "testm", 5.5) });
            parsedMetrics = PrometheusMetricsParser.ParseMetrics(dateTime, temp).ToArray();
            Assert.Single(parsedMetrics);

            temp = temp.Replace("5.5", "not_a_number");
            parsedMetrics = PrometheusMetricsParser.ParseMetrics(dateTime, temp).ToArray();
            Assert.Empty(parsedMetrics);
        }

        private string GenerateFakeScrape(IEnumerable<(string name, string module, double value)> data)
        {
            string dataPoints = string.Join("\n", data.Select(d => $@"
{d.name}{{iothub=""fakeHub"",edge_device=""fakeDevice"",instance_number=""1"",blank_tag="""",module_name=""{d.module}""}} {d.value}
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
