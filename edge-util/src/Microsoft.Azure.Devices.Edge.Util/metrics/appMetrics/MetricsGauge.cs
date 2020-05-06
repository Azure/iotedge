// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util.Metrics.AppMetrics
{
    using System.Collections.Generic;
    using App.Metrics;
    using App.Metrics.Gauge;

    public class MetricsGauge : BaseMetric, IMetricsGauge
    {
        readonly IMeasureGaugeMetrics gaugeMetrics;
        readonly GaugeOptions gaugeOptions;

        public MetricsGauge(string name, IMeasureGaugeMetrics gaugeMetrics, List<string> labelNames)
            : base(labelNames, new List<string>())
        {
            this.gaugeMetrics = gaugeMetrics;
            this.gaugeOptions = new GaugeOptions
            {
                Name = name,
                MeasurementUnit = Unit.Items
            };
        }

        public void Set(double value, string[] labelValues)
        {
            var tags = new MetricTags(this.LabelNames, labelValues);
            this.gaugeMetrics.SetValue(this.gaugeOptions, tags, value);
        }
    }
}
