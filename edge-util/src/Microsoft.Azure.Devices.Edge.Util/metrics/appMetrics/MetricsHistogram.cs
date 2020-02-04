// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics.AppMetrics
{
    using System.Collections.Generic;
    using App.Metrics;
    using App.Metrics.Histogram;

    public class MetricsHistogram : BaseMetric, IMetricsHistogram
    {
        readonly IMeasureHistogramMetrics histogramMetrics;
        readonly HistogramOptions histogramOptions;

        public MetricsHistogram(string name, IMeasureHistogramMetrics histogramMetrics, List<string> labelNames)
            : base(labelNames, new List<string>())
        {
            this.histogramMetrics = histogramMetrics;
            this.histogramOptions = new HistogramOptions
            {
                Name = name,
                MeasurementUnit = Unit.Items
            };
        }

        // Note, this will convert value to a long, since AppMetrics histograms only support longs.
        public void Update(double value, string[] labelValues)
        {
            var tags = new MetricTags(this.LabelNames, labelValues);
            this.histogramMetrics.Update(this.histogramOptions, MetricTags.Concat(MetricTags.Empty, tags), (long)value);
        }
    }
}
