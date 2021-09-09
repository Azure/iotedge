// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Service
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Autofac;
    using Microsoft.Azure.Devices.Edge.Agent.Diagnostics;
    using Microsoft.Azure.Devices.Edge.Agent.Diagnostics.Publisher;
    using Microsoft.Azure.Devices.Edge.Agent.Diagnostics.Storage;
    using Microsoft.Azure.Devices.Edge.Storage;
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

            // Task<IMetricsStorage>
            builder.Register(async c =>
                {
                    IStoreProvider storeProvider = await c.Resolve<Task<IStoreProvider>>();
                    ISequentialStore<IEnumerable<Metric>> dataStore = await storeProvider.GetSequentialStore<IEnumerable<Metric>>("Metrics");

                    return new MetricsStorage(dataStore, this.diagnosticConfig.MaxUploadAge) as IMetricsStorage;
                })
                .As<Task<IMetricsStorage>>()
                .SingleInstance();

            // IMetricsPublisher
            builder.RegisterType<EdgeRuntimeDiagnosticsUpload>()
                .As<IMetricsPublisher>()
                .SingleInstance();

            // Task<MetricsWorker>
            builder.Register(async c =>
                {
                    IMetricsScraper scraper = c.Resolve<IMetricsScraper>();
                    IMetricsPublisher publisher = c.Resolve<IMetricsPublisher>();
                    Task<IMetricsStorage> storage = c.Resolve<Task<IMetricsStorage>>();

                    return new MetricsWorker(scraper, await storage, publisher);
                })
                .As<Task<MetricsWorker>>()
                .SingleInstance();

            base.Load(builder);
        }
    }
}
