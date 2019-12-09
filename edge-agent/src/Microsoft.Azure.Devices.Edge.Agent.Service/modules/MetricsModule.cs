// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Service
{
    using System.IO;
    using Autofac;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Metrics;
    using Microsoft.Azure.Devices.Edge.Util.Metrics.NullMetrics;
    using Microsoft.Azure.Devices.Edge.Util.Metrics.Prometheus.Net;
    using Microsoft.Extensions.Logging;

    public sealed class MetricsModule : Module
    {
        readonly ILogger logger = Logger.Factory.CreateLogger<MetricsModule>();
        MetricsConfig metricsConfig;
        string iothubHostname;
        string deviceId;
        string edgeAgentStorageFolder;

        public MetricsModule(MetricsConfig metricsConfig, string iothubHostname, string deviceId, string edgeAgentStorageFolder)
        {
            Preconditions.CheckNotNull(edgeAgentStorageFolder, nameof(edgeAgentStorageFolder));
            this.edgeAgentStorageFolder = edgeAgentStorageFolder;

            if (!Directory.Exists(edgeAgentStorageFolder))
            {
                this.logger.LogError($"Edge Agent storage directory at {edgeAgentStorageFolder} not found. Disabling metrics.");
                this.metricsConfig = new MetricsConfig(false, metricsConfig.ListenerConfig);
            }
            else
            {
                this.metricsConfig = Preconditions.CheckNotNull(metricsConfig, nameof(metricsConfig));
            }

            this.iothubHostname = Preconditions.CheckNonWhiteSpace(iothubHostname, nameof(iothubHostname));
            this.deviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
        }

        protected override void Load(ContainerBuilder builder)
        {
            builder.Register(c => this.metricsConfig.Enabled ?
                                new MetricsProvider(Constants.EdgeAgentModuleName, this.iothubHostname, this.deviceId, this.edgeAgentStorageFolder) :
                                new NullMetricsProvider() as IMetricsProvider)
                .As<IMetricsProvider>()
                .SingleInstance();

            builder.Register(c => this.metricsConfig.Enabled ?
                                new Util.Metrics.Prometheus.Net.MetricsListener(this.metricsConfig.ListenerConfig, c.Resolve<IMetricsProvider>()) :
                                new Util.Metrics.NullMetrics.NullMetricsListener() as IMetricsListener)
                .As<IMetricsListener>()
                .SingleInstance();

            base.Load(builder);
        }
    }
}
