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
    public class PrometheusMetricsParserTest
    {
        private static readonly DateTime testTime = DateTime.UnixEpoch;

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

        [Fact]
        public void TestParsesZero()
        {
            int n = 10;

            /* test data */
            (string name, string module, double value)[] fakeScrape = Enumerable.Range(1, n).SelectMany(i => Enumerable.Range(100, n).Select(j => ($"metric_{i}", $"module_{j}", 0.0))).ToArray();
            DateTime dateTime = DateTime.UtcNow;

            Metric[] parsedMetrics = PrometheusMetricsParser.ParseMetrics(dateTime, this.GenerateFakeScrape(fakeScrape)).ToArray();
            Assert.NotEmpty(parsedMetrics);

            foreach (var metric in parsedMetrics)
            {
                Assert.Equal(0, metric.Value);
            }
        }

        [Fact]
        public void TestEscapedQuotesInLabelValues()
        {
            IEnumerable<Metric> metrics =
                PrometheusMetricsParser.ParseMetrics(
                    testTime,
                    "metricname{edge_device=\"any number of quotes should be fine \\\"\\\"\\\"\\\"\\\"\"} 0");
            Assert.Single(metrics);
            Assert.Equal(0, metrics.First().Value);
            Assert.Equal("metricname", metrics.First().Name);
            Assert.Single(metrics.First().Tags);
            Assert.Equal("any number of quotes should be fine \"\"\"\"\"", metrics.First().Tags["edge_device"]);

            metrics = PrometheusMetricsParser.ParseMetrics(testTime, "metricname{metricname=\"this \\\"is a metric\\\" value\",edge_device=\"any number of quotes should be fine \\\"\\\"\\\"\\\"\\\"\"} 0");
            Assert.Single(metrics);
            Assert.Equal(0, metrics.First().Value);
            Assert.Equal("metricname", metrics.First().Name);
            Assert.Equal(2, metrics.First().Tags.Count());
            Assert.Equal("this \"is a metric\" value", metrics.First().Tags["metricname"]);
            Assert.Equal("any number of quotes should be fine \"\"\"\"\"", metrics.First().Tags["edge_device"]);

            metrics = PrometheusMetricsParser.ParseMetrics(testTime, @"edgeAgent_metadata{iothub=""vh2.azure-devices.net"",edge_device=""rer"",instance_number=""eb060182-ecb2-4903-9793-385766a8a951"",edge_agent_version=""1.0.10-rc2.34217022 (029016ef1bf82dec749161d95c6b73aa5ee9baf1)"",experimental_features=""{\""Enabled\"":false,\""DisableCloudSubscriptions\"":false}"",host_information=""{\""OperatingSystemType\"":\""linux\"",\""Architecture\"":\""x86_64\"",\""Version\"":\""1.0.10~rc2\"",\""ServerVersion\"":\""19.03.12+azure\"",\""KernelVersion\"":\""5.4.0-26-generic\"",\""OperatingSystem\"":\""Ubuntu 20.04 LTS\"",\""NumCpus\"":4}"",ms_telemetry=""True""} 0");
            Assert.Single(metrics);
            Assert.Equal(0, metrics.First().Value);
            Assert.Equal("edgeAgent_metadata", metrics.First().Name);
            Assert.Equal(7, metrics.First().Tags.Count());
            Assert.Equal("vh2.azure-devices.net", metrics.First().Tags["iothub"]);
            Assert.Equal("rer", metrics.First().Tags["edge_device"]);
            Assert.Equal("eb060182-ecb2-4903-9793-385766a8a951", metrics.First().Tags["instance_number"]);
            Assert.Equal("1.0.10-rc2.34217022 (029016ef1bf82dec749161d95c6b73aa5ee9baf1)", metrics.First().Tags["edge_agent_version"]);
            Assert.Equal("{\"Enabled\":false,\"DisableCloudSubscriptions\":false}", metrics.First().Tags["experimental_features"]);
            Assert.Equal(
                "{" +
                    "\"OperatingSystemType\":\"linux\",\"Architecture\":\"x86_64\"," +
                    "\"Version\":\"1.0.10~rc2\",\"ServerVersion\":\"19.03.12+azure\"," +
                    "\"KernelVersion\":\"5.4.0-26-generic\",\"OperatingSystem\":\"Ubuntu 20.04 LTS\",\"NumCpus\":4" +
                "}",
                metrics.First().Tags["host_information"]);
            Assert.Equal("True", metrics.First().Tags["ms_telemetry"]);
        }

        [Fact]
        public void TestExponentialNotation()
        {
            IEnumerable<Metric> metrics = PrometheusMetricsParser.ParseMetrics(testTime, "edgeAgent_metadata{} -1E-06");
            Assert.Single(metrics);
            Assert.Equal(-0.000001, metrics.First().Value);
            Assert.Equal("edgeAgent_metadata", metrics.First().Name);
            Assert.Empty(metrics.First().Tags);

            metrics = PrometheusMetricsParser.ParseMetrics(testTime, "edgeAgent_metadata{} 2E-1");
            Assert.Single(metrics);
            Assert.Equal(0.2, metrics.First().Value);
            Assert.Equal("edgeAgent_metadata", metrics.First().Name);
            Assert.Empty(metrics.First().Tags);

            metrics = PrometheusMetricsParser.ParseMetrics(testTime, "edgeAgent_metadata{} 3e-1");
            Assert.Single(metrics);
            Assert.Equal(0.3, metrics.First().Value);
            Assert.Equal("edgeAgent_metadata", metrics.First().Name);
            Assert.Empty(metrics.First().Tags);

            metrics = PrometheusMetricsParser.ParseMetrics(testTime, "edgeAgent_metadata{} 4E-0");
            Assert.Single(metrics);
            Assert.Equal(4, metrics.First().Value);
            Assert.Equal("edgeAgent_metadata", metrics.First().Name);
            Assert.Empty(metrics.First().Tags);

            metrics = PrometheusMetricsParser.ParseMetrics(testTime, "edgeAgent_metadata{} -5E0");
            Assert.Single(metrics);
            Assert.Equal(-5, metrics.First().Value);
            Assert.Equal("edgeAgent_metadata", metrics.First().Name);
            Assert.Empty(metrics.First().Tags);

            metrics = PrometheusMetricsParser.ParseMetrics(testTime, "edgeAgent_metadata{} 6e0");
            Assert.Single(metrics);
            Assert.Equal(6, metrics.First().Value);
            Assert.Equal("edgeAgent_metadata", metrics.First().Name);
            Assert.Empty(metrics.First().Tags);

            metrics = PrometheusMetricsParser.ParseMetrics(testTime, "edgeAgent_metadata{} 7E+0");
            Assert.Single(metrics);
            Assert.Equal(7, metrics.First().Value);
            Assert.Equal("edgeAgent_metadata", metrics.First().Name);
            Assert.Empty(metrics.First().Tags);

            metrics = PrometheusMetricsParser.ParseMetrics(testTime, "edgeAgent_metadata{} 8E06");
            Assert.Single(metrics);
            Assert.Equal(8000000, metrics.First().Value);
            Assert.Equal("edgeAgent_metadata", metrics.First().Name);
            Assert.Empty(metrics.First().Tags);

            metrics = PrometheusMetricsParser.ParseMetrics(testTime, "edgeAgent_metadata{} -9E+06");
            Assert.Single(metrics);
            Assert.Equal(-9000000, metrics.First().Value);
            Assert.Equal("edgeAgent_metadata", metrics.First().Name);
            Assert.Empty(metrics.First().Tags);

            metrics = PrometheusMetricsParser.ParseMetrics(testTime, "edgeAgent_metadata{} 5e+06");
            Assert.Single(metrics);
            Assert.Equal(5000000, metrics.First().Value);
            Assert.Equal("edgeAgent_metadata", metrics.First().Name);
            Assert.Empty(metrics.First().Tags);
        }

        [Fact]
        public void TestNaN()
        {
            IEnumerable<Metric> metrics = PrometheusMetricsParser.ParseMetrics(testTime, @"edgeAgent_command_latency_seconds{iothub=""somevalue.net""} NaN");
            Assert.Single(metrics);
            Assert.Equal("edgeAgent_command_latency_seconds", metrics.First().Name);
            Assert.Single(metrics.First().Tags);
            Assert.Equal("somevalue.net", metrics.First().Tags["iothub"]);
            Assert.True(double.IsNaN(metrics.First().Value));
        }

        [Fact]
        public void TestInterstitialWhitespace()
        {
            IEnumerable<Metric> metrics =
                PrometheusMetricsParser.ParseMetrics(
                    testTime,
                    "edgeAgent_metadata \t { \t iothub \t = \t \" \t vh2.azure \\n -devices.net \t \" \t }55 66");
            Assert.Single(metrics);
            Assert.Equal("edgeAgent_metadata", metrics.First().Name);
            Assert.Single(metrics.First().Tags);
            Assert.Equal(" \t vh2.azure \n -devices.net \t ", metrics.First().Tags["iothub"]);
            Assert.Equal(55, metrics.First().Value);
            Assert.Equal(DateTime.UnixEpoch + TimeSpan.FromMilliseconds(66), metrics.First().TimeGeneratedUtc);

            metrics =
                PrometheusMetricsParser.ParseMetrics(
                    testTime,
                    "edgeAgent_metadata \t { \t iothub \t = \t \" \t vh2.azure \\n -devices.net \t \" \t , \t } \t 55 \t 66");
            Assert.Single(metrics);
            Assert.Equal("edgeAgent_metadata", metrics.First().Name);
            Assert.Single(metrics.First().Tags);
            Assert.Equal(" \t vh2.azure \n -devices.net \t ", metrics.First().Tags["iothub"]);
            Assert.Equal(55, metrics.First().Value);
            Assert.Equal(DateTime.UnixEpoch + TimeSpan.FromMilliseconds(66), metrics.First().TimeGeneratedUtc);

            metrics =
                PrometheusMetricsParser.ParseMetrics(
                    testTime,
                    "edgeAgent_metadata \t {" +
                        " \t iothub \t = \t \" \t vh2.azure \\n -devices.net \t \" \t ," +
                        " \t edge_device \t = \t \" \t rer \t \" \t ," +
                        " \t " +
                    "} \t 55 \t 66");
            Assert.Single(metrics);
            Assert.Equal("edgeAgent_metadata", metrics.First().Name);
            Assert.Equal(2, metrics.First().Tags.Count());
            Assert.Equal(" \t vh2.azure \n -devices.net \t ", metrics.First().Tags["iothub"]);
            Assert.Equal(" \t rer \t ", metrics.First().Tags["edge_device"]);
            Assert.Equal(55, metrics.First().Value);
            Assert.Equal(DateTime.UnixEpoch + TimeSpan.FromMilliseconds(66), metrics.First().TimeGeneratedUtc);
        }

        [Fact]
        public void TestTrailingCommaAfterLabelValue()
        {
            IEnumerable<Metric> metrics =
                PrometheusMetricsParser.ParseMetrics(
                    testTime,
                    @"edgeAgent_metadata{iothub=""vh2.azure-devices.net"",}55 66");
            Assert.Single(metrics);
            Assert.Equal("edgeAgent_metadata", metrics.First().Name);
            Assert.Single(metrics.First().Tags);
            Assert.Equal("vh2.azure-devices.net", metrics.First().Tags["iothub"]);
            Assert.Equal(55, metrics.First().Value);
            Assert.Equal(DateTime.UnixEpoch + TimeSpan.FromMilliseconds(66), metrics.First().TimeGeneratedUtc);

            metrics =
                PrometheusMetricsParser.ParseMetrics(
                    testTime,
                    @"edgeAgent_metadata{iothub=""vh2.azure-devices.net"",edge_device=""rer"",}55 66");
            Assert.Single(metrics);
            Assert.Equal("edgeAgent_metadata", metrics.First().Name);
            Assert.Equal(2, metrics.First().Tags.Count());
            Assert.Equal("vh2.azure-devices.net", metrics.First().Tags["iothub"]);
            Assert.Equal("rer", metrics.First().Tags["edge_device"]);
            Assert.Equal(55, metrics.First().Value);
            Assert.Equal(DateTime.UnixEpoch + TimeSpan.FromMilliseconds(66), metrics.First().TimeGeneratedUtc);
        }

        [Fact]
        public void TestInterstitialWhitespaceAndTrailingCommaAndEscapesInLabelValues()
        {
            IEnumerable<Metric> metrics =
                PrometheusMetricsParser.ParseMetrics(
                    testTime,
                    "edgeAgent_metadata \t { \t iothub \t = \t \" \t vh2.azure \\\" \\\\ \\n -devices.net \t \" \t , \t }55 66");
            Assert.Single(metrics);
            Assert.Equal("edgeAgent_metadata", metrics.First().Name);
            Assert.Single(metrics.First().Tags);
            Assert.Equal(" \t vh2.azure \" \\ \n -devices.net \t ", metrics.First().Tags["iothub"]);
            Assert.Equal(55, metrics.First().Value);
            Assert.Equal(DateTime.UnixEpoch + TimeSpan.FromMilliseconds(66), metrics.First().TimeGeneratedUtc);

            metrics =
                PrometheusMetricsParser.ParseMetrics(
                    testTime,
                    "edgeAgent_metadata \t { \t iothub \t = \t \" \t vh2.azure \\\" \\\\ \\n -devices.net \t \" \t , \t } \t 55 \t 66");
            Assert.Single(metrics);
            Assert.Equal("edgeAgent_metadata", metrics.First().Name);
            Assert.Single(metrics.First().Tags);
            Assert.Equal(" \t vh2.azure \" \\ \n -devices.net \t ", metrics.First().Tags["iothub"]);
            Assert.Equal(55, metrics.First().Value);
            Assert.Equal(DateTime.UnixEpoch + TimeSpan.FromMilliseconds(66), metrics.First().TimeGeneratedUtc);

            metrics =
                PrometheusMetricsParser.ParseMetrics(
                    testTime,
                    "edgeAgent_metadata \t {" +
                        " \t iothub \t = \t \" \t vh2.azure \\\" \\\\ \\n -devices.net \t \" \t ," +
                        " \t edge_device \t = \t \" \t rer \t \" \t ," +
                        " \t " +
                    "} \t 55 \t 66");
            Assert.Single(metrics);
            Assert.Equal("edgeAgent_metadata", metrics.First().Name);
            Assert.Equal(2, metrics.First().Tags.Count());
            Assert.Equal(" \t vh2.azure \" \\ \n -devices.net \t ", metrics.First().Tags["iothub"]);
            Assert.Equal(" \t rer \t ", metrics.First().Tags["edge_device"]);
            Assert.Equal(55, metrics.First().Value);
            Assert.Equal(DateTime.UnixEpoch + TimeSpan.FromMilliseconds(66), metrics.First().TimeGeneratedUtc);
        }

        [Fact]
        public void TestInvalidMetrics()
        {
            // Leading whitespace is invalid
            IEnumerable<Metric> metrics =
                PrometheusMetricsParser.ParseMetrics(
                    testTime,
                    " \t edgeAgent_metadata{iothub=\"vh2.azure-devices.net\",edge_device=\"rer\"} 5");
            Assert.Empty(metrics);

            // Only \", \\ and \n escapes are allowed in tag values.
            metrics =
                PrometheusMetricsParser.ParseMetrics(
                    testTime,
                    @"edgeAgent_metadata{iothub=""vh2.azure \? -devices.net"",edge_device=""rer""} 5");
            Assert.Empty(metrics);

            // If there is a metric value and no metric timestamp, there should be no whitespace after the metric value.
            metrics =
                PrometheusMetricsParser.ParseMetrics(
                    testTime,
                    "edgeAgent_metadata{iothub=\"vh2.azure-devices.net\",edge_device=\"rer\"} 5 \t ");
            Assert.Empty(metrics);

            // If there is a metric timestamp, there should be no whitespace after the metric timestamp.
            metrics =
                PrometheusMetricsParser.ParseMetrics(
                    testTime,
                    "edgeAgent_metadata{iothub=\"vh2.azure-devices.net\",edge_device=\"rer\"} 5 \t 6 \t ");
            Assert.Empty(metrics);
        }
    }
}
