
namespace Microsoft.Azure.Devices.Edge.Util.Metrics.AppMetrics
{
    using System;
    using System.Collections.Generic;
    using App.Metrics;
    using App.Metrics.Counter;
    using App.Metrics.Gauge;

    public class MetricsGauge : IMetricsGauge
    {
        readonly IMeasureGaugeMetrics gaugeMetrics;
        readonly GaugeOptions gaugeOptions;

        public MetricsGauge(string name, IMeasureGaugeMetrics gaugeMetrics, Dictionary<string, string> defaultTags)
        {
            this.gaugeMetrics = gaugeMetrics;
            MetricTags defaultMetricsTags = MetricTags.Concat(MetricTags.Empty, defaultTags);
            this.gaugeOptions = new GaugeOptions
            {
                Name = name,
                MeasurementUnit = Unit.Items,
                Tags = defaultMetricsTags
            };
        }

        public void Set(long value)
        {
            this.gaugeMetrics.SetValue(this.gaugeOptions, value);
        }

        public void Set(long value, Dictionary<string, string> tags)
        {
            this.gaugeMetrics.SetValue(this.gaugeOptions, MetricTags.Concat(MetricTags.Empty, tags), value);
        }
    }
}
