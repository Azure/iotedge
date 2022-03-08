namespace Microsoft.Azure.Devices.Edge.Azure.Monitor
{
    public static class Constants
    {
        public static readonly string VersionNumber = "0.1.2.0";  // TODO: grab this from somewhere else
        public static readonly string MetricOrigin = "iot.azm.ms";
        public static readonly string MetricNamespace = "metricsmodule";
        public static readonly string MetricComputer = "";  // TODO: maybe put a short value here? Customers pay for the storage though...
        public static readonly string MetricUploadIPName = "IotInsights";  // We don't think log analytics uses this string. If anything maybe for billing?
        public static readonly string MetricUploadDataType = "INSIGHTS_METRICS_BLOB";
        public static readonly string IoTUploadMessageIdentifier = "origin-iotedge-metrics-collector";
        public static readonly int UploadMaxRetries = 3;
        public const string DefaultLogAnalyticsWorkspaceDomainPrefixOds = ".ods.opinsights.";
        public const string DefaultLogAnalyticsWorkspaceDomainPrefixOms = ".oms.opinsights.";
        public const string ProductInfo = "IoTEdgeMetricsCollectorModule";
    }
}
