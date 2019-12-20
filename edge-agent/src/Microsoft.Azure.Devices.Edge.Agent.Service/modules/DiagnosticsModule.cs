// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Service
{
    using Autofac;
    using Microsoft.Azure.Devices.Edge.Agent.Diagnostics;
    using Microsoft.Azure.Devices.Edge.Agent.Diagnostics.Publisher;
    using Microsoft.Azure.Devices.Edge.Agent.Diagnostics.Storage;
    using Microsoft.Azure.Devices.Edge.Util;

    public sealed class DiagnosticsModule : Module
    {
        DiagnosticConfig diagnosticConfig;

        public DiagnosticsModule(DiagnosticConfig diagnosticConfig)
        {
            this.diagnosticConfig = Preconditions.CheckNotNull(diagnosticConfig, nameof(diagnosticConfig));
        }

        protected override void Load(ContainerBuilder builder)
        {
            // IMetricsScraper
            builder.Register(c => new MetricsScraper(new string[] { "http://edgeHub:9600/metrics", "http://edgeAgent:9600/metrics" }))
                .As<IMetricsScraper>()
                .SingleInstance();

            // IMetricsStorage
            builder.RegisterType<MetricsStorage>()
                .As<IMetricsStorage>()
                .SingleInstance();

            // IMetricsPublisher
            builder.RegisterType<EdgeRuntimeDiagnosticsUpload>()
                .As<IMetricsPublisher>()
                .SingleInstance();

            // MetricsWorker
            builder.RegisterType<MetricsWorker>()
                .SingleInstance();

            base.Load(builder);
        }
    }
}
