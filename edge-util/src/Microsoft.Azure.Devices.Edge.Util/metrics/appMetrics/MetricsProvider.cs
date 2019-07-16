// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics.AppMetrics
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using App.Metrics;
    using App.Metrics.Formatters;

    public class MetricsProvider : IMetricsProvider
    {
        readonly IMetricsRoot metricsRoot;

        MetricsProvider(IMetricsRoot metricsRoot)
        {
            this.metricsRoot = metricsRoot;
        }

        public static MetricsProvider Create(string deviceId)
        {
            IMetricsRoot metricsRoot = new MetricsBuilder()
                .Configuration.Configure(options =>
                {
                    options.DefaultContextLabel = MetricsConstants.EdgeHubMetricPrefix;
                    options.GlobalTags.Add(MetricsConstants.DeviceIdLabel, deviceId);
                })
                .OutputMetrics.AsPrometheusPlainText()
                .Build();
            var metricsProvider = new MetricsProvider(metricsRoot);
            return metricsProvider;
        }

        public IMetricsCounter CreateCounter(string name, string description, List<string> labelNames)
            => new MetricsCounter(name, this.metricsRoot.Measure.Counter, labelNames);

        public IMetricsGauge CreateGauge(string name, string description, List<string> labelNames)
            => new MetricsGauge(name, this.metricsRoot.Measure.Gauge, labelNames);

        public IMetricsHistogram CreateHistogram(string name, string description, List<string> labelNames)
            => new MetricsHistogram(name, this.metricsRoot.Measure.Histogram, labelNames);

        public IMetricsDuration CreateDuration(string name, string description, List<string> labelNames)
            => new MetricsDuration(name, DurationUnit.Seconds, this.metricsRoot.Measure.Histogram, labelNames);

        public IMetricsTimer CreateTimer(string name, string description, List<string> labelNames)
            => new MetricsTimer(name, this.metricsRoot.Measure.Timer, labelNames);

        public async Task<byte[]> GetSnapshot(CancellationToken cancellationToken)
        {
            using (var ms = new MemoryStream())
            {
                MetricsDataValueSource metricsData = this.metricsRoot.Snapshot.Get();
                IMetricsOutputFormatter formatter = this.metricsRoot.DefaultOutputMetricsFormatter;
                await formatter.WriteAsync(ms, metricsData, cancellationToken);
                return ms.ToArray();
            }
        }
    }
}
