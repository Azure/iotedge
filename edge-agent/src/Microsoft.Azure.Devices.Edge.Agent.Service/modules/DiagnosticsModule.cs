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
        string storagePath;

        public DiagnosticsModule(string storagePath)
        {
            this.storagePath = Path.Combine("metrics", storagePath);
        }

        protected override void Load(ContainerBuilder builder)
        {
            // IScraper
            builder.Register(c => new MetricsScraper(new string[] { "http://edgeHub:9600/metrics", "http://edgeAgent:9600/metrics" }))
                .As<IMetricsScraper>()
                .SingleInstance();

            // IFileStorage
            builder.Register(c => new MetricsFileStorage(this.storagePath))
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
