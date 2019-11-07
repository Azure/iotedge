// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Service
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using Autofac;
    using Microsoft.Azure.Devices.Edge.Agent.DiagnosticsComponent;
    using Microsoft.Extensions.Logging;

    public class DiagnosticsModule : Module
    {
        DiagnosticConfig diagnosticConfig;

        public DiagnosticsModule(DiagnosticConfig diagnosticConfig)
        {
            this.diagnosticConfig = diagnosticConfig;
        }

        protected override void Load(ContainerBuilder builder)
        {
            // IScraper
            builder.Register(c => new MetricsScraper(new string[] { "http://edgeHub:9600/metrics", "http://edgeAgent:9600/metrics" }))
                .As<IMetricsScraper>()
                .SingleInstance();

            // IFileStorage
            builder.Register(c => new MetricsFileStorage(this.diagnosticConfig.MetricsStoragePath))
                .As<IMetricsStorage>()
                .SingleInstance();

            // IMetricsUpload
            builder.Register(c => new FileWriter())
                .As<IMetricsUpload>()
                .SingleInstance();

            // MetricsWorker
            builder.RegisterType<MetricsWorker>()
                .SingleInstance();

            base.Load(builder);
        }
    }
}
