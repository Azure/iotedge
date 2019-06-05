// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics.AppMetrics
{
    using System.Collections.Generic;
    using App.Metrics;
    using App.Metrics.Meter;

    public class MetricsMeter : IMetricsMeter
    {
        readonly IMeasureMeterMetrics meterMetrics;
        readonly MeterOptions meterOptions;

        public MetricsMeter(string name, IMeasureMeterMetrics meterMetrics, Dictionary<string, string> defaultTags)
        {
            this.meterMetrics = meterMetrics;
            MetricTags defaultMetricsTags = MetricTags.Concat(MetricTags.Empty, defaultTags);
            this.meterOptions = new MeterOptions
            {
                Name = name,
                MeasurementUnit = Unit.Items,
                Tags = defaultMetricsTags
            };
        }

        public void Mark() => this.meterMetrics.Mark(this.meterOptions);

        public void Mark(long count) => this.meterMetrics.Mark(this.meterOptions, count);

        public void Mark(Dictionary<string, string> tags) => this.meterMetrics.Mark(this.meterOptions, MetricTags.Concat(MetricTags.Empty, tags));

        public void Mark(long count, Dictionary<string, string> tags) => this.meterMetrics.Mark(this.meterOptions, MetricTags.Concat(MetricTags.Empty, tags), count);
    }
}
