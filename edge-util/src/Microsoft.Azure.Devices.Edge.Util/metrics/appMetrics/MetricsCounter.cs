// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics.AppMetrics
{
    using System.Collections.Generic;
    using System.Linq;
    using App.Metrics;
    using App.Metrics.Counter;
    using ICounter = Microsoft.Azure.Devices.Edge.Util.Metrics.ICounter;

    public class MetricsCounter : ICounter
    {
        readonly IMeasureCounterMetrics counterMetrics;
        readonly MetricTags defaultTags;
        readonly CounterOptions counterOptions;

        public MetricsCounter(string name, IMeasureCounterMetrics counterMetrics, Dictionary<string, string> defaultTags)
        {
            this.counterMetrics = counterMetrics;
            this.defaultTags = MetricTags.Concat(MetricTags.Empty, defaultTags);
            this.counterOptions = new CounterOptions
            {
                Name = name,
                MeasurementUnit = Unit.Items
            };
        }

        public void Increment(long amount) => throw new System.NotImplementedException();

        public void Decrement(long amount) => throw new System.NotImplementedException();

        public void Increment(long amount, Dictionary<string, string> tags)
        {
            var metricTags = MetricTags.Concat(this.defaultTags, tags);
            this.counterMetrics.Increment(this.counterOptions, metricTags, amount);
        }

        public void Decrement(long amount, Dictionary<string, string> tags) => throw new System.NotImplementedException();
    }
}
