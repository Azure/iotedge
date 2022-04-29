using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Microsoft.Azure.Devices.Edge.Azure.Monitor.Test
{
    public class MiscellaneousTests
    {
        private readonly static DateTime testTime = DateTime.UnixEpoch;


        // These tests have been added as a part of the fix for a Partner bug where filtering based on AllowedMetrics doesn't work correctly.
        // Unit tests exist for both the MetricFilter and for the PrometheusParser. However, some bugs were not caught during testing
        // because there isn't a functional test that verifies that the outcome of the interaction between the PrometheusParser and the
        // MetricFilter is what is actually desired.

        // The Azure Monitor code does not lend itself to mocking (using Dependency Injection) -- Too many changes need to be made in
        // classes that are not meant to be changed. "MiscellaneousTests" seemed to be the most reasonable way to test the offending
        // code: MetricsScrapeAndUpload.ScrapeAndUploadMetricsAsync()

        // The only difference between TestSingleEndpointFilter() & TestMultipleEndpointFilter() is exactly what the names suggest.
        // They've been split into separate tests for ease of maintainability.

        [Fact]
        public void TestSingleEndpointFilter()
        {
            // this is an actual message (forcibly logged for test purposes) from the Azure Monitor code.
            string prometheusMessage = "total_network_out_bytes{iothub=\"IoTHub.azure-devices.net\",edge_device=\"Ubuntu-20\",instance_number=\"b9e51990-f3f9-4d90-8fc7-ef62d01929b2\",module_name=\"edgeHub\",ms_telemetry=\"False\"} 1683388"
                + System.Environment.NewLine
                + "total_network_out_bytes{iothub =\"IoTHub.azure-devices.net\",edge_device=\"Ubuntu-20\",instance_number=\"b9e51990-f3f9-4d90-8fc7-ef62d01929b2\",module_name=\"edgeAgent\",ms_telemetry=\"False\"} 5134109"
                + System.Environment.NewLine
                + "edgehub_client_connect_success_total{iothub =\"IoTHub.azure-devices.net\",edge_device=\"Ubuntu-20\",instance_number=\"023163b5-1db5-41bc-b57e-4c4d1047d9c9\",id=\"DeviceId: Ubuntu-20; ModuleId: IoTEdgeMetricsCollector [IotHubHostName: IoTHub.azure-devices.net]\"} 2"
                + System.Environment.NewLine
                + "edgehub_client_connect_success_total{iothub =\"IoTHub.azure-devices.net\",edge_device=\"Ubuntu-20\",instance_number=\"023163b5-1db5-41bc-b57e-4c4d1047d9c9\",id=\"DeviceId: Ubuntu-20; ModuleId: $edgeHub [IotHubHostName: IoTHub.azure-devices.net]\"} 1";

            MetricFilter filter = new MetricFilter("total_network_out_bytes{edge_device=\"Ubuntu-20\"}[http://VeryNoisyModule:9001/metrics]");

            IEnumerable<Metric> metrics = PrometheusMetricsParser.ParseMetrics(testTime, prometheusMessage);

            // verify this fails (despite metric & label matching) because endpoint isn't specified
            metrics = metrics.Where(x => filter.Matches(x));
            Assert.Empty(metrics);

            // added endpoint
            metrics = PrometheusMetricsParser.ParseMetrics(testTime, prometheusMessage, "http://VeryNoisyModule:9001/metrics");

            // the code upto this point was mostly setup; now, we test a few scenarios:

            // verify metric that doesn't exist isn't matched: edgehub_gettwin_total is a built-in metric, but hasn't been included in the "prometheusMessage" above.
            filter = new MetricFilter("edgehub_gettwin_total{edge_device=\"Ubuntu-20\"}[http://VeryNoisyModule:9001/metrics]");
            metrics = metrics.Where(x => filter.Matches(x));
            Assert.Empty(metrics);

            // verify that metric filters correctly
            filter = new MetricFilter("total_network_out_bytes{instance_number=\"b9e51990-f3f9-4d90-8fc7-ef62d01929b2\"}[http://VeryNoisyModule:9001/metrics]");
            metrics = PrometheusMetricsParser.ParseMetrics(testTime, prometheusMessage, "http://VeryNoisyModule:9001/metrics");
            metrics = metrics.Where(x => filter.Matches(x));
            Assert.True(metrics.Count() == 2);
            Assert.Equal("http://VeryNoisyModule:9001/metrics", metrics.ElementAt(0).Endpoint);
            Assert.Equal("total_network_out_bytes", metrics.ElementAt(0).Name);
        }

        [Fact]
        public void TestMultipleEndpointFilter()
        {
            string prometheusMessage2 = "total_network_out_bytes{iothub=\"IoTHub.azure-devices.net\",edge_device=\"Ubuntu-20\",instance_number=\"b9e51990-f3f9-4d90-8fc7-ef62d01929b2\",module_name=\"edgeHub\",ms_telemetry=\"False\"} 1683388"
               + System.Environment.NewLine
               + "total_network_out_bytes{iothub =\"IoTHub.azure-devices.net\",edge_device=\"Ubuntu-20\",instance_number=\"b9e51990-f3f9-4d90-8fc7-ef62d01929b2\",module_name=\"edgeAgent\",ms_telemetry=\"False\"} 5134109"
               + System.Environment.NewLine;

            string prometheusMessage3 = "total_network_out_bytes{iothub =\"IoTHub.azure-devices.net\",edge_device=\"Ubuntu-20\",instance_number=\"023163b5-1db5-41bc-b57e-4c4d1047d9c9\",id=\"DeviceId: Ubuntu-20; ModuleId: IoTEdgeMetricsCollector [IotHubHostName: IoTHub.azure-devices.net]\"} 2"
               + System.Environment.NewLine
               + "total_network_out_bytes{iothub =\"IoTHub.azure-devices.net\",edge_device=\"Ubuntu-20\",instance_number=\"023163b5-1db5-41bc-b57e-4c4d1047d9c9\",id=\"DeviceId: Ubuntu-20; ModuleId: $edgeHub [IotHubHostName: IoTHub.azure-devices.net]\"} 1";


            // The rationale here was there are some default metrics (like .NET perf metrics) have the same name and a customer could be interested in
            // allowing metrics from one module but not others. Inclusion of the endpoint name in the AllowedMetrics helps in that scenario.

            IEnumerable<Metric> metrics2 = PrometheusMetricsParser.ParseMetrics(testTime, prometheusMessage2, "http://VeryNoisyModule:9001/metrics");
            IEnumerable<Metric> metrics3 = PrometheusMetricsParser.ParseMetrics(testTime, prometheusMessage3, "http://Google:9001/metrics");

            IEnumerable<Metric> bigList = metrics2.Concat(metrics3);

            MetricFilter filter = new MetricFilter("total_network_out_bytes{edge_device=\"Ubuntu-20\"}[http://VeryNoisyModule:9001/metrics]");
            bigList = bigList.Where(x => filter.Matches(x));
            Assert.True(bigList.Count() == 2);

            // next test -- just metrics, no labels
            string prometheusMessage4 = "total_network_out_bytes{iothub=\"IoTHub.azure-devices.net\",edge_device=\"Ubuntu-20\",instance_number=\"b9e51990-f3f9-4d90-8fc7-ef62d01929b2\",module_name=\"edgeHub\",ms_telemetry=\"False\"} 1683388"
               + System.Environment.NewLine
               + "edgehub_gettwin_total{iothub =\"IoTHub.azure-devices.net\",edge_device=\"Ubuntu-20\",instance_number=\"b9e51990-f3f9-4d90-8fc7-ef62d01929b2\",module_name=\"edgeAgent\",ms_telemetry=\"False\"} 5134109"
               + System.Environment.NewLine;

            string prometheusMessage5 = "total_network_out_bytes{iothub =\"IoTHub.azure-devices.net\",edge_device=\"Ubuntu-20\",instance_number=\"023163b5-1db5-41bc-b57e-4c4d1047d9c9\",id=\"DeviceId: Ubuntu-20; ModuleId: IoTEdgeMetricsCollector [IotHubHostName: IoTHub.azure-devices.net]\"} 2"
               + System.Environment.NewLine
               + "edgehub_gettwin_total{iothub =\"IoTHub.azure-devices.net\",edge_device=\"Ubuntu-21\",instance_number=\"023163b5-1db5-41bc-b57e-4c4d1047d9c9\",id=\"DeviceId: Ubuntu-20; ModuleId: $edgeHub [IotHubHostName: IoTHub.azure-devices.net]\"} 1";

            IEnumerable<Metric> metrics4 = PrometheusMetricsParser.ParseMetrics(testTime, prometheusMessage4, "http://VeryNoisyModule:9001/metrics");
            IEnumerable<Metric> metrics5 = PrometheusMetricsParser.ParseMetrics(testTime, prometheusMessage5, "http://Google:9001/metrics");

            bigList = metrics4.Concat(metrics5);

            filter = new MetricFilter("total_network_out_bytes edgehub_gettwin_total");
            bigList = bigList.Where(x => filter.Matches(x));
            Assert.True(bigList.Count() == 4);

            // add condition on one endpoint, but not the other
            filter = new MetricFilter("total_network_out_bytes edgehub_gettwin_total{edge_device=\"Ubuntu-21\"}");
            bigList = bigList.Where(x => filter.Matches(x));
            Assert.True(bigList.Count() == 3);
        }

        // Label Equality & regex tests -- added for completeness and as a safeguard against future changes (and therefore, potential bugs)

        [Fact]
        public void TestWildcard()
        {
            string prometheusMessage = "edgehub_gettwin_total{iothub=\"IoTHub.azure-devices.net\",edge_device=\"Ubuntu-20\",instance_number=\"b9e51990-f3f9-4d90-8fc7-ef62d01929b2\",module_name=\"edgeHub\",ms_telemetry=\"False\"} 1683388"
                + System.Environment.NewLine
                + "total_network_out_bytes{iothub =\"IoTHub.azure-devices.net\",edge_device=\"Ubuntu-20\",instance_number=\"b9e51990-f3f9-4d90-8fc7-ef62d01929b2\",module_name=\"edgeAgent\",ms_telemetry=\"False\"} 5134109"
                + System.Environment.NewLine
                + "edgehub_gettwin_total{iothub =\"IoTHub.azure-devices.net\",edge_device=\"Ubuntu-21\",instance_number=\"023163b5-1db5-41bc-b57e-4c4d1047d9c9\",id=\"DeviceId: Ubuntu-20; ModuleId: IoTEdgeMetricsCollector [IotHubHostName: IoTHub.azure-devices.net]\"} 2"
                + System.Environment.NewLine
                + "max_network_out_bytes{iothub =\"IoTHub.azure-devices.net\",edge_device=\"Ubuntu-20\",instance_number=\"023163b5-1db5-41bc-b57e-4c4d1047d9c9\",id=\"DeviceId: Ubuntu-20; ModuleId: $edgeHub [IotHubHostName: IoTHub.azure-devices.net]\"} 1";

            MetricFilter filter = new MetricFilter("*");

            IEnumerable<Metric> metrics = PrometheusMetricsParser.ParseMetrics(testTime, prometheusMessage, "http://VeryNoisyModule:9001/metrics");

            IEnumerable<Metric> result = metrics.Where(x => filter.Matches(x));
            Assert.True(result.Count() == 4);

            // Wildcards * (any characters) and ? (any single character) can be used in metric names.
            // For example, *_out_bytes would match max_network_out_bytes and total_network_out_bytes (but not network_out_bytes_max).

            filter = new MetricFilter("*_out_bytes");
            result = metrics.Where(x => filter.Matches(x));
            Assert.True(result.Count() == 2);

            // will match max_network_out_bytes, but not total_network_out_bytes
            // ? refers to a single character in our implementation, not 1 or 'more than 1'.
            filter = new MetricFilter("???_network_out_bytes");
            result = metrics.Where(x => filter.Matches(x));
            Assert.True(result.Count() == 1);

            // will match total_network_out_bytes, but not max_network_out_bytes
            // ? refers to a single character in our implementation, not 0 or 1.
            filter = new MetricFilter("?????_network_out_bytes");
            result = metrics.Where(x => filter.Matches(x));
            Assert.True(result.Count() == 1);
        }

        [Fact]
        public void TestLabelMatch()
        {
            string prometheusMessage = "edgehub_gettwin_total{iothub=\"IoTHub.azure-devices.net\",edge_device=\"Ubuntu-20\",instance_number=\"b9e51990-f3f9-4d90-8fc7-ef62d01929b2\",module_name=\"edgeHub\",ms_telemetry=\"False\"} 1683388"
                + System.Environment.NewLine
                + "edgehub_gettwin_total{iothub =\"IoTHub.azure-devices.net\",edge_device=\"Ubuntu-20\",instance_number=\"b9e51990-f3f9-4d90-8fc7-ef62d01929b2\",module_name=\"edgeAgent\",ms_telemetry=\"False\"} 5134109"
                + System.Environment.NewLine
                + "edgehub_gettwin_total{iothub =\"IoTHub.azure-devices.net\",edge_device=\"Ubuntu-21\",instance_number=\"023163b5-1db5-41bc-b57e-4c4d1047d9c9\",id=\"DeviceId: Ubuntu-20; ModuleId: IoTEdgeMetricsCollector [IotHubHostName: IoTHub.azure-devices.net]\"} 2"
                + System.Environment.NewLine
                + "edgehub_gettwin_total{iothub =\"IoTHub.azure-devices.net\",edge_device=\"Ubuntu-20\",instance_number=\"023163b5-1db5-41bc-b57e-4c4d1047d9c9\",id=\"DeviceId: Ubuntu-20; ModuleId: $edgeHub [IotHubHostName: IoTHub.azure-devices.net]\"} 1";

            // Like PromQL, the following matching operators are allowed.
            // != Match labels not exactly equal to the provided string.
            MetricFilter filter = new MetricFilter("edgehub_gettwin_total{edge_device!=\"Ubuntu-20\"}");

            IEnumerable<Metric> metrics = PrometheusMetricsParser.ParseMetrics(testTime, prometheusMessage, "http://VeryNoisyModule:9001/metrics");

            IEnumerable<Metric> result = metrics.Where(x => filter.Matches(x));
            Assert.True(result.Count() == 1);
            Assert.Contains("Ubuntu-21", result.ElementAt(0).Tags.Values);

            // = Match labels exactly equal to the provided string(case sensitive).
            filter = new MetricFilter("edgehub_gettwin_total{instance_number=\"b9e51990-f3f9-4d90-8fc7-ef62d01929b2\"}");
            result = metrics.Where(x => filter.Matches(x));
            Assert.True(result.Count() == 2);

            // Multiple metric values can be included in the curly brackets. The values should be comma-separated.
            // A metric will be matched if at least all labels in the selector are present and also match.
            filter = new MetricFilter("edgehub_gettwin_total{instance_number=\"023163b5-1db5-41bc-b57e-4c4d1047d9c9\", edge_device!=\"Ubuntu-20\"}");
            result = metrics.Where(x => filter.Matches(x));
            Assert.True(result.Count() == 1);
        }

        [Fact]
        public void RegexTest()
        {
            string prometheusMessage = "edgehub_gettwin_total{iothub=\"IoTHub.azure-devices.net\",edge_device=\"Ubuntu-20\",instance_number=\"b9e51990-f3f9-4d90-8fc7-ef62d01929b2\",module_name=\"edgeHub\",ms_telemetry=\"False\"} 1683388"
                + System.Environment.NewLine
                + "edgehub_gettwin_total{iothub =\"IoTHub.azure-devices.net\",edge_device=\"2012Wrong\",KingKong_SSN=\"!@!\",instance_number=\"b9e51990-f3f9-4d90-8fc7-ef62d01929b2\",module_name=\"edgeAgent\",ms_telemetry=\"False\"} 5134109"
                + System.Environment.NewLine
                + "edgehub_gettwin_total{iothub =\"IoTHub.azure-devices.net\",edge_device=\"Ubuntu-21\",instance_number=\"023163b5-1db5-41bc-b57e-4c4d1047d9c9\",id=\"DeviceId: Ubuntu-20; ModuleId: IoTEdgeMetricsCollector [IotHubHostName: IoTHub.azure-devices.net]\"} 2"
                + System.Environment.NewLine
                + "edgehub_gettwin_total{iothub =\"IoTHub.azure-devices.net\",edge_device=\"Ubuntu-20\",KingKong_SSN=\"63b5-1db5-41\",id=\"DeviceId: Ubuntu-20; ModuleId: $edgeHub [IotHubHostName: IoTHub.azure-devices.net]\"} 1";

            // Like PromQL, the following matching operators are allowed
            // Future maintainability note: The leading hypen '-' in the regexes below matters, easy to miss though it may be.

            // = ~Match labels to a provided regex
            MetricFilter filter = new MetricFilter("edgehub_gettwin_total{KingKong_SSN=~\"[-a-zA-Z0-9_]*\"}");

            IEnumerable <Metric> metrics = PrometheusMetricsParser.ParseMetrics(testTime, prometheusMessage, "http://VeryNoisyModule:9001/metrics");

            IEnumerable<Metric> result = metrics.Where(x => filter.Matches(x));
            Assert.True(result.Count() == 1);

            // != Match labels that don't fit a provided regex.
            filter = new MetricFilter("edgehub_gettwin_total{KingKong_SSN!~\"[-a-zA-Z0-9_]*\"}");

            result = metrics.Where(x => filter.Matches(x));
            Assert.True(result.Count() == 1);
        }

    }
}
