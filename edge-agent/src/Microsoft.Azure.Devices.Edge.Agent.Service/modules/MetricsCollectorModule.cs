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

    public class MetricsCollectorModule : Module
    {
        string storagePath;

        public MetricsCollectorModule(string storagePath)
        {
            this.storagePath = Path.Combine("metrics", storagePath);
        }

        protected override void Load(ContainerBuilder builder)
        {
            // IScraper
            builder.Register(c => new Scraper(new string[] { "http://edgeHub:9600/metrics", "http://edgeAgent:9600/metrics" }))
                .As<IMetricsScraper>()
                .SingleInstance();

            // IFileStorage
            builder.Register(c => new MetricsFileStorage(this.storagePath))
                .As<IMetricsFileStorage>()
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
