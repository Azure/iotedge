// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Diagnostics
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Akka.Event;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    /// <summary>
    /// This class acts as a pass through for a group of metrics.
    /// It will aggrigate metrics that share a given tag.
    /// </summary>
    public class MetricAggrigator
    {
        List<(string tag, Func<double, double, double> aggrigationFunc)> tagsToAggrigate;

        public MetricAggrigator(params (string tag, Func<double, double, double> aggrigationFunc)[] modifications)
        {
            this.tagsToAggrigate = modifications.ToList();
        }

        public IEnumerable<Metric> AggrigateMetrics(IEnumerable<Metric> metrics)
        {
            foreach (var (tag, func) in this.tagsToAggrigate)
            {
                metrics = this.AggregateTag(tag, func, metrics);
            }

            return metrics;
        }

        IEnumerable<Metric> AggregateTag(string tag, Func<double, double, double> aggrigationFunc, IEnumerable<Metric> metrics)
        {
            var aggrigateValues = new Dictionary<AggrigateMetric, double>();
            foreach (Metric metric in metrics)
            {
                if (metric.Tags.ContainsKey(tag))
                {
                    var aggrigateMetric = new AggrigateMetric(metric, tag);

                    if (aggrigateValues.TryGetValue(aggrigateMetric, out double value))
                    {
                        aggrigateValues[aggrigateMetric] = aggrigationFunc(value, metric.Value);
                    }
                    else
                    {
                        aggrigateValues.Add(aggrigateMetric, metric.Value);
                    }
                }
                else
                {
                    yield return metric;
                }
            }

            foreach (var aggrigate in aggrigateValues)
            {
                yield return aggrigate.Key.ToMetric(aggrigate.Value);
            }
        }

        class AggrigateMetric
        {
            public DateTime TimeGeneratedUtc { get; }
            public string Name { get; }
            public IReadOnlyDictionary<string, string> Tags { get; }

            int? hash = null;

            public AggrigateMetric(Metric metric, string aggrigateTag)
            {
                this.Name = metric.Name;
                this.TimeGeneratedUtc = metric.TimeGeneratedUtc;
                this.Tags = metric.Tags.Where(t => t.Key != aggrigateTag).ToDictionary(t => t.Key, t => t.Value);
            }

            public Metric ToMetric(double value)
            {
                return new Metric(this.TimeGeneratedUtc, this.Name, value, this.Tags);
            }

            public override bool Equals(object other)
            {
                return this.GetHashCode() == other.GetHashCode();
            }

            public override int GetHashCode()
            {
                return this.hash ?? this.Hash();
            }

            int Hash()
            {
                this.hash = HashCode.Combine(this.Name.GetHashCode(), this.TimeGeneratedUtc.GetHashCode(), HashCode.Combine(this.Tags.Select(o => HashCode.Combine(o.Key.GetHashCode(), o.Value.GetHashCode())).OrderBy(h => h).ToArray()));

                return (int)this.hash;
            }
        }
    }
}
