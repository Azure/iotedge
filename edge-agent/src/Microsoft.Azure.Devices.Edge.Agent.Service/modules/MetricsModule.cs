// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Service
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Autofac;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Metrics;
    using Microsoft.Azure.Devices.Edge.Util.Metrics.NullMetrics;
    using Microsoft.Azure.Devices.Edge.Util.Metrics.Prometheus.Net;

    public sealed class MetricsModule : Module
    {
        MetricsConfig metricsConfig;
        string iothubHostname;
        string deviceId;
        string edgeAgentStorageFolder;
        string apiVersion;

        public MetricsModule(MetricsConfig metricsConfig, string iothubHostname, string deviceId, string edgeAgentStorageFolder, string apiVersion)
        {
            this.metricsConfig = Preconditions.CheckNotNull(metricsConfig, nameof(metricsConfig));
            this.iothubHostname = Preconditions.CheckNotNull(iothubHostname, nameof(iothubHostname));
            this.deviceId = Preconditions.CheckNotNull(deviceId, nameof(deviceId));
            if (!Directory.Exists(Preconditions.CheckNotNull(edgeAgentStorageFolder, nameof(edgeAgentStorageFolder))))
            {
                throw new ArgumentException("Edge Agent storage folder not defined");
            }

            this.edgeAgentStorageFolder = edgeAgentStorageFolder;
        }

        protected override void Load(ContainerBuilder builder)
        {
            builder.Register(c => this.metricsConfig.Enabled ?
                                new MetricsProvider(Constants.EdgeAgentModuleName, this.iothubHostname, this.deviceId, this.edgeAgentStorageFolder) :
                                new NullMetricsProvider() as IMetricsProvider)
                .As<IMetricsProvider>()
                .SingleInstance();

            builder.Register(c => new Util.Metrics.Prometheus.Net.MetricsListener(this.metricsConfig.ListenerConfig, c.Resolve<IMetricsProvider>()))
                .As<IMetricsListener>()
                .SingleInstance();

            builder.Register(c => new SystemResourcesMetrics(c.Resolve<IMetricsProvider>(), c.Resolve<IModuleManager>().GetSystemResourcesAsync, this.apiVersion))
                            .SingleInstance();

            base.Load(builder);
        }
    }
}
