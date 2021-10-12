using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Microsoft.Azure.Devices.Edge.Azure.Monitor.Test
{
    public class MiscellaneousTests
    {
        private readonly static DateTime testTime = DateTime.UnixEpoch;


        // This test has been added as a part of the fix for Partner bug where filtering based on AllowedMetrics doesn't work correctly.
        // Unit tests exist for both the MetricFilter and for the PrometheusParser. However, the reason this bug wasn't caught during
        // testing is because there isn't a functional test that verifies that the outcome of the interaction between the
        // PrometheusParser and the MetricFilter is what is desired.

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
        }
    }
}
