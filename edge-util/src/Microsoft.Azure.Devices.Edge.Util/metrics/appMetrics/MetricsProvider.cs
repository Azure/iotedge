// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics.AppMetrics
{
    using System.Collections.Generic;
    using App.Metrics;

    public class MetricsProvider : IMetricsProvider
    {
        readonly IMetricsRoot metricsRoot;
        readonly MetricsListener metricsListener;

        MetricsProvider(IMetricsRoot metricsRoot, MetricsListener metricsListener)
        {
            this.metricsRoot = metricsRoot;
            this.metricsListener = metricsListener;
        }

        public static MetricsProvider CreatePrometheusExporter(string url)
        {
            IMetricsRoot metricsRoot = new MetricsBuilder()
                .OutputMetrics.AsPrometheusPlainText()
                .Build();
            var metricsListener = new MetricsListener(url, metricsRoot);
            var metricsProvider = new MetricsProvider(metricsRoot, metricsListener);
            return metricsProvider;
        }

        public ICounter CreateCounter(string name, Dictionary<string, string> tags) =>
            new MetricsCounter(name, this.metricsRoot.Measure.Counter, tags);

        public IMetricsGauge CreateGauge(string name, Dictionary<string, string> defaultTags)
            => new MetricsGauge(name, this.metricsRoot.Measure.Gauge, defaultTags);

        public IMetricsHistogram CreateHistogram(string name, Dictionary<string, string> defaultTags)
            => new MetricsHistogram(name, this.metricsRoot.Measure.Histogram, defaultTags);

        public IMetricsMeter CreateMeter(string name, Dictionary<string, string> defaultTags)
            => new MetricsMeter(name, this.metricsRoot.Measure.Meter, defaultTags);

        public IMetricsTimer CreateTimer(string name, Dictionary<string, string> defaultTags)
            => new MetricsTimer(name, this.metricsRoot.Measure.Timer, defaultTags);
    }
}
