// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics.Prometheus.Net
{
    using System.Collections.Generic;
    using global::Prometheus;

    public class MetricsGauge : BaseMetric, IMetricsGauge
    {
        readonly Gauge gauge;

        public MetricsGauge(string name, string description, List<string> labelNames, List<string> defaultLabelValues)
            : base(labelNames, defaultLabelValues)
        {
            this.gauge = Metrics.CreateGauge(name, description, labelNames.ToArray());
        }

        public double Get(string[] labelValues) => this.gauge.WithLabels(this.GetLabelValues(labelValues)).Value;

        public void Set(double value, string[] labelValues)
            => this.gauge
                .WithLabels(this.GetLabelValues(labelValues))
                .Set(value);

        public void Increment(string[] labelValues) => this.gauge.WithLabels(this.GetLabelValues(labelValues)).Inc();

        public void Decrement(string[] labelValues) => this.gauge.WithLabels(this.GetLabelValues(labelValues)).Dec();
    }
}
