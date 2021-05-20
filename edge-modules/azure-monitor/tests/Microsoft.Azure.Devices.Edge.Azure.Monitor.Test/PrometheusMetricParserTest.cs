// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

// This is more of a regression test than 

namespace Microsoft.Azure.Devices.Edge.Azure.Monitor.Test
{
    public class PrometheusMetricParserTest
    {
        private readonly static DateTime testTime = DateTime.UnixEpoch;

        [Fact]
        public void TestEmpty()
        {
            IEnumerable<Metric> metrics = PrometheusMetricsParser.ParseMetrics(testTime, "");
            Assert.Empty(metrics);
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
        public void TestTime()
        {
            IEnumerable<Metric> metrics = PrometheusMetricsParser.ParseMetrics(testTime, "metricName{} NaN");
            Assert.Single(metrics);
            Assert.True(Double.IsNaN(metrics.First().Value));
            Assert.Equal(testTime, metrics.First().TimeGeneratedUtc);

            DateTime testTime2 = new DateTime(2020,2,3,4,5,6,7,System.DateTimeKind.Utc);

            metrics = PrometheusMetricsParser.ParseMetrics(testTime2, "metricName{m=\"a\", n=\"b\"} 5.678");
            Assert.Single(metrics);
            Assert.Equal(5.678, metrics.First().Value);
            Assert.Equal(testTime2, metrics.First().TimeGeneratedUtc);
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
        public void TestZero()
        {
            IEnumerable<Metric> metrics = PrometheusMetricsParser.ParseMetrics(testTime, "metricname{} 0");
            Assert.Single(metrics);
            Assert.Equal(0, metrics.First().Value);
            Assert.Equal("metricname", metrics.First().Name);
            Assert.Empty(metrics.First().Tags);
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
    }
}
