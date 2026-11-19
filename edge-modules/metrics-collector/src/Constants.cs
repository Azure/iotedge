namespace Microsoft.Azure.Devices.Edge.Azure.Monitor
{
    public static class Constants
    {
        public static readonly string VersionNumber = "0.1.2.0";  // TODO: grab this from somewhere else
        public static readonly string MetricOrigin = "iot.azm.ms";
        public static readonly string MetricNamespace = "metricsmodule";
        public static readonly string IoTUploadMessageIdentifier = "origin-iotedge-metrics-collector";
        public static readonly int UploadMaxRetries = 3;
        public const string ProductInfo = "IoTEdgeMetricsCollectorModule";
    }
}
