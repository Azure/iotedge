// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Service
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Autofac;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Metrics;
    using Microsoft.Azure.Devices.Edge.Util.Metrics;
    using Microsoft.Azure.Devices.Edge.Util.Metrics.NullMetrics;
    using Microsoft.Azure.Devices.Edge.Util.Metrics.Prometheus.Net;

    public class MetricsModule : Autofac.Module
    {
        MetricsConfig metricsConfig;
        string iothubHostname;
        string deviceId;

        public MetricsModule(MetricsConfig metricsConfig, string iothubHostname, string deviceId)
        {
            this.metricsConfig = metricsConfig;
            this.iothubHostname = iothubHostname;
            this.deviceId = deviceId;
        }

        protected override void Load(ContainerBuilder builder)
        {
            builder.Register(c => this.metricsConfig.Enabled
                                 ? new MetricsProvider("edgeagent", this.iothubHostname, this.deviceId)
                                 : new NullMetricsProvider() as IMetricsProvider)
                .As<IMetricsProvider>()
                .SingleInstance();

            builder.Register(c => this.metricsConfig.Enabled
                                 ? new Util.Metrics.Prometheus.Net.MetricsListener(this.metricsConfig.ListenerConfig, c.Resolve<IMetricsProvider>())
                                 : new NullMetricsListener() as IMetricsListener)
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
        }
    }
}
