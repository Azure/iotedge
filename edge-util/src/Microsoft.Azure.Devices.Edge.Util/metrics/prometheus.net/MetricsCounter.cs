// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics.Prometheus.Net
{
    using System.Collections.Generic;
    using global::Prometheus;

    public class MetricsCounter : BaseMetric, IMetricsCounter
    {
        readonly Counter counter;

        public MetricsCounter(string name, string description, List<string> labelNames, List<string> defaultLabelValues)
            : base(labelNames, defaultLabelValues)
        {
            this.counter = Metrics.CreateCounter(name, description, labelNames.ToArray());
        }

        public void Increment(long count, string[] labelValues)
            => this.counter.WithLabels(this.GetLabelValues(labelValues)).Inc(count);
    }
}
