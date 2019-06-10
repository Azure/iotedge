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
        const string EdgeHubLabel = "edge_hub";
        const string DeviceIdTag = "edge_device";

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
                    options.DefaultContextLabel = EdgeHubLabel;
                    options.GlobalTags.Add(DeviceIdTag, deviceId);
                })
                .OutputMetrics.AsPrometheusPlainText()
                .Build();
            var metricsProvider = new MetricsProvider(metricsRoot);
            return metricsProvider;
        }

        public IMetricsCounter CreateCounter(string name, Dictionary<string, string> tags) =>
            new MetricsCounter(name, this.metricsRoot.Measure.Counter, tags);

        public IMetricsGauge CreateGauge(string name, Dictionary<string, string> defaultTags)
            => new MetricsGauge(name, this.metricsRoot.Measure.Gauge, defaultTags);

        public IMetricsHistogram CreateHistogram(string name, Dictionary<string, string> defaultTags)
            => new MetricsHistogram(name, this.metricsRoot.Measure.Histogram, defaultTags);

        public IMetricsMeter CreateMeter(string name, Dictionary<string, string> defaultTags)
            => new MetricsMeter(name, this.metricsRoot.Measure.Meter, defaultTags);

        public IMetricsTimer CreateTimer(string name, Dictionary<string, string> defaultTags)
            => new MetricsTimer(name, this.metricsRoot.Measure.Timer, defaultTags);

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
