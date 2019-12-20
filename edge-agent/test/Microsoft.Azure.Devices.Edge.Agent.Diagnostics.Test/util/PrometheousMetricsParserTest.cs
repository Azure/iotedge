// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Diagnostics.Test.Util
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Microsoft.Azure.Devices.Edge.Agent.Diagnostics.Util;
    using Newtonsoft.Json;
    using Xunit;

    public class PrometheousMetricsParserTest
    {
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
