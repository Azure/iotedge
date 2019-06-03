// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics.AppMetrics
{
    using System;
    using System.Collections.Generic;
    using App.Metrics;
    using App.Metrics.Counter;

    public class MetricsCounter : IMetricsCounter
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

        public void Increment(long amount) => throw new NotImplementedException();

        public void Decrement(long amount) => throw new NotImplementedException();

        public void Increment(long amount, Dictionary<string, string> tags)
        {
            var metricTags = MetricTags.Concat(this.defaultTags, tags);
            this.counterMetrics.Increment(this.counterOptions, metricTags, amount);
        }

        public void Decrement(long amount, Dictionary<string, string> tags) => throw new NotImplementedException();
    }
}
