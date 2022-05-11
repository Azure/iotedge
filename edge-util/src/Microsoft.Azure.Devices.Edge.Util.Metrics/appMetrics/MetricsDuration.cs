// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics.AppMetrics
{
    using System.Collections.Generic;
    using App.Metrics;
    using App.Metrics.Histogram;

    public class MetricsDuration : BaseMetric, IMetricsDuration
    {
        readonly IMeasureHistogramMetrics histogramMetrics;
        readonly HistogramOptions histogramOptions;
        readonly DurationUnit durationUnit;

        public MetricsDuration(string name, DurationUnit durationUnit, IMeasureHistogramMetrics histogramMetrics, List<string> labelNames)
            : base(labelNames, new List<string>())
        {
            this.histogramMetrics = histogramMetrics;
            this.histogramOptions = new HistogramOptions
            {
                Name = $"{name}_milliseconds",
                MeasurementUnit = Unit.Items
            };
            this.durationUnit = durationUnit;
        }

        public void Set(double value, string[] labelValues)
        {
            long millisecondValue = (long)(this.durationUnit == DurationUnit.Seconds ? value * 1000 : value);
            var tags = new MetricTags(this.LabelNames, labelValues);
            this.histogramMetrics.Update(this.histogramOptions, MetricTags.Concat(MetricTags.Empty, tags), millisecondValue);
        }
    }
}
