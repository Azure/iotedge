// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics.Prometheus.Net
{
    using System.Globalization;
    using global::Prometheus;
    using Microsoft.Extensions.Logging;

    public class MetricsListener : IMetricsListener
    {
        const string MetricsUrlPrefixFormat = "http://{0}:{1}/{2}/";

        readonly MetricServer metricServer;
        readonly ILogger logger;
        readonly string url;

        public MetricsListener(string host, int port, string suffix, ILogger logger)
        {
            this.metricServer = new MetricServer(host, port, suffix.Trim('/') + '/');
            this.logger = logger;
            this.url = GetMetricsListenerUrlPrefix(host, port, suffix);
        }

        public void Dispose()
        {
            this.logger.LogInformation("Stopping metrics listener");
            this.metricServer.Stop();
        }

        public void Start()
        {
            this.logger.LogInformation($"Starting metrics listener on {this.url}");
            this.metricServer.Start();
        }

        static string GetMetricsListenerUrlPrefix(string host, int port, string urlSuffix)
            => string.Format(CultureInfo.InvariantCulture, MetricsUrlPrefixFormat, host, port.ToString(), urlSuffix.Trim('/', ' '));
    }
}
