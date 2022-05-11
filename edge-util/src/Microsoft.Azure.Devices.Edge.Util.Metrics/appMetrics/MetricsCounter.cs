// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics.AppMetrics
{
    using System.Collections.Generic;
    using App.Metrics;
    using App.Metrics.Counter;

    public class MetricsCounter : BaseMetric, IMetricsCounter
    {
        readonly IMeasureCounterMetrics counterMetrics;
        readonly CounterOptions counterOptions;

        public MetricsCounter(string name, IMeasureCounterMetrics counterMetrics, List<string> labelNames)
            : base(labelNames, new List<string>())
        {
            this.counterMetrics = counterMetrics;
            this.counterOptions = new CounterOptions
            {
                Name = name
            };
        }

        public void Increment(long count, string[] labelValues)
        {
            var tags = new MetricTags(this.LabelNames, labelValues);
            this.counterMetrics.Increment(this.counterOptions, tags, count);
        }
    }
}
