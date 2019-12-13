// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Service
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using Autofac;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Agent.Diagnostics;
    using Microsoft.Azure.Devices.Edge.Agent.Diagnostics.Publisher;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

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
            builder.Register(c => new MetricsFileStorage(this.diagnosticConfig.MetricsStoragePath))
                .As<IMetricsStorage>()
                .SingleInstance();

            // IMetricsPublisher
            builder.RegisterType<IoTHubMetricsUpload>()
                .As<IMetricsPublisher>()
                .SingleInstance();

            // MetricsWorker
            builder.RegisterType<MetricsWorker>()
                .SingleInstance();

            base.Load(builder);
        }
    }
}
