// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics.AppMetrics
{
    using System.Collections.Generic;
    using App.Metrics;
    using App.Metrics.Histogram;

    public class MetricsHistogram : IMetricsHistogram
    {
        readonly IMeasureHistogramMetrics histogramMetrics;
        readonly HistogramOptions histogramOptions;

        public MetricsHistogram(string name, IMeasureHistogramMetrics histogramMetrics, Dictionary<string, string> defaultTags)
        {
            this.histogramMetrics = histogramMetrics;
            MetricTags defaultMetricsTags = MetricTags.Concat(MetricTags.Empty, defaultTags);
            this.histogramOptions = new HistogramOptions
            {
                Name = name,
                MeasurementUnit = Unit.Items,
                Tags = defaultMetricsTags
            };
        }

        public void Update(long value)
        {
            this.histogramMetrics.Update(this.histogramOptions, value);
        }

        public void Update(long value, Dictionary<string, string> tags)
        {
            this.histogramMetrics.Update(this.histogramOptions, MetricTags.Concat(MetricTags.Empty, tags), value);
        }
    }
}
