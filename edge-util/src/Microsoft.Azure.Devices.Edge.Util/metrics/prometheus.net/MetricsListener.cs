// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics.Prometheus.Net
{
    using System;
    using global::Prometheus;
    using Microsoft.Extensions.Logging;

    public class MetricsListener : IMetricsListener
    {
        readonly MetricServer metricServer;
        readonly MetricsListenerConfig listenerConfig;

        ILogger logger;

        public MetricsListener(MetricsListenerConfig listenerConfig, IMetricsProvider metricsProvider)
        {
            if (!(metricsProvider is MetricsProvider prometheusMetricsProvider))
            {
                throw new ArgumentException($"IMetricsProvider of type {metricsProvider.GetType()} is incompatible with {this.GetType()}");
            }

            this.listenerConfig = Preconditions.CheckNotNull(listenerConfig, nameof(listenerConfig));
            this.metricServer = new MetricServer(listenerConfig.Host, listenerConfig.Port, listenerConfig.Suffix.Trim('/') + '/', prometheusMetricsProvider.DefaultRegistry);
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
