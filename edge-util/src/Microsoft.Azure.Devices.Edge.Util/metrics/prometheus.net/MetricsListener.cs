// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics.Prometheus.Net
{
    using global::Prometheus;
    using Microsoft.Extensions.Logging;

    public class MetricsListener : IMetricsListener
    {
        readonly MetricServer metricServer;
        readonly MetricsListenerConfig listenerConfig;

        ILogger logger;

        public MetricsListener(MetricsListenerConfig listenerConfig)
        {
            this.listenerConfig = Preconditions.CheckNotNull(listenerConfig, nameof(listenerConfig));
            this.metricServer = new MetricServer(listenerConfig.Host, listenerConfig.Port, listenerConfig.Suffix.Trim('/') + '/');
        }

        public void Dispose()
        {
            this.logger?.LogInformation("Stopping metrics listener");
            this.metricServer.Stop();
        }

        public void Start(ILogger logger)
        {
            this.logger = logger;
            this.logger?.LogInformation($"Starting metrics listener on {this.listenerConfig}");
            this.metricServer.Start();
        }
    }
}
