// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Service
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Autofac;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Metrics;
    using Microsoft.Azure.Devices.Edge.Agent.Edgelet;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Metrics;
    using Microsoft.Azure.Devices.Edge.Util.Metrics.NullMetrics;
    using Microsoft.Azure.Devices.Edge.Util.Metrics.Prometheus.Net;

    public class MetricsModule : Autofac.Module
    {
        MetricsConfig metricsConfig;
        string iothubHostname;
        string deviceId;
        string apiVersion;

        public MetricsModule(MetricsConfig metricsConfig, string iothubHostname, string deviceId, string apiVersion)
        {
            this.metricsConfig = Preconditions.CheckNotNull(metricsConfig, nameof(metricsConfig));
            this.iothubHostname = Preconditions.CheckNotNull(iothubHostname, nameof(iothubHostname));
            this.deviceId = Preconditions.CheckNotNull(deviceId, nameof(deviceId));
            this.apiVersion = Preconditions.CheckNotNull(apiVersion, nameof(apiVersion));
        }

        protected override void Load(ContainerBuilder builder)
        {
            builder.Register(c => new MetricsProvider("edgeagent", this.iothubHostname, this.deviceId))
                .As<IMetricsProvider>()
                .SingleInstance();

            builder.Register(c => new Util.Metrics.Prometheus.Net.MetricsListener(this.metricsConfig.ListenerConfig, c.Resolve<IMetricsProvider>()))
                .As<IMetricsListener>()
                .SingleInstance();

            Dictionary<Type, string> recognizedExceptions = new Dictionary<Type, string>
            {
                // TODO: Decide what exceptions to recognize and ignore
                { typeof(Newtonsoft.Json.JsonSerializationException), "json_serialization" },
                { typeof(ArgumentException), "argument" },
                { typeof(Rest.HttpOperationException), "http" },
            };
            HashSet<Type> ignoredExceptions = new HashSet<Type>
            {
                typeof(TaskCanceledException),
                typeof(OperationCanceledException),
            };
            builder.Register(c => new ExceptionCounter(recognizedExceptions, ignoredExceptions, c.Resolve<IMetricsProvider>()))
                .SingleInstance();

            builder.Register(c => new SystemResourcesMetrics(c.Resolve<IMetricsProvider>(), c.Resolve<IModuleManager>().GetSystemResourcesAsync, this.apiVersion))
                .SingleInstance();

            base.Load(builder);
        }
    }
}
