// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics.Prometheus.Net
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Prometheus;

    public class MetricsProvider : IMetricsProvider
    {
        const string CounterNameFormat = "{0}_{1}_total";
        const string NameFormat = "{0}_{1}";
        readonly string namePrefix;
        readonly List<string> defaultLabelNames;

        public MetricsProvider(string namePrefix, string iotHubName, string deviceId)
        {
            this.namePrefix = Preconditions.CheckNonWhiteSpace(namePrefix, nameof(namePrefix));
            Preconditions.CheckNonWhiteSpace(iotHubName, nameof(iotHubName));
            Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            this.defaultLabelNames = new List<string> { iotHubName, deviceId, Guid.NewGuid().ToString() };

            // TODO:
            // By default, the Prometheus.Net library emits some default metrics.
            // While useful, these are emitted without any tags. This will make it hard to
            // consume and make sense of these metrics. So suppressing the default metrics for
            // now. We can look at ways to add tags to the default metrics, or emiting the
            // metrics manually.
            Metrics.SuppressDefaultMetrics();
        }

        public IMetricsGauge CreateGauge(string name, string description, List<string> labelNames)
            => new MetricsGauge(this.GetName(name), description, this.GetLabelNames(labelNames), this.defaultLabelNames);

        public IMetricsCounter CreateCounter(string name, string description, List<string> labelNames)
            => new MetricsCounter(this.GetCounterName(name), description, this.GetLabelNames(labelNames), this.defaultLabelNames);

        public IMetricsTimer CreateTimer(string name, string description, List<string> labelNames)
            => new MetricsTimer(this.GetName(name), description, this.GetLabelNames(labelNames), this.defaultLabelNames);

        public IMetricsHistogram CreateHistogram(string name, string description, List<string> labelNames)
            => new MetricsHistogram(this.GetName(name), description, this.GetLabelNames(labelNames), this.defaultLabelNames);

        public IMetricsDuration CreateDuration(string name, string description, List<string> labelNames)
            => new MetricsDuration(this.GetName(name), description, this.GetLabelNames(labelNames), this.defaultLabelNames);

        public async Task<byte[]> GetSnapshot(CancellationToken cancellationToken)
        {
            using (var ms = new MemoryStream())
            {
                await Metrics.DefaultRegistry.CollectAndExportAsTextAsync(ms, cancellationToken);
                return ms.ToArray();
            }
        }

        internal CollectorRegistry DefaultRegistry => Metrics.DefaultRegistry;

        string GetCounterName(string name) => string.Format(CultureInfo.InvariantCulture, CounterNameFormat, this.namePrefix, name);

        string GetName(string name) => string.Format(CultureInfo.InvariantCulture, NameFormat, this.namePrefix, name);

        List<string> GetLabelNames(List<string> labelNames)
        {
            var allLabelNames = new List<string>
            {
                MetricsConstants.IotHubLabel,
                MetricsConstants.DeviceIdLabel,
                "instance_number"
            };
            allLabelNames.AddRange(labelNames);
            return allLabelNames;
        }
    }
}
