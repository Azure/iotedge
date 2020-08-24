// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Diagnostics.Util.Aggrigation
{
    using System;
    using System.Collections;
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
        HashSet<string> metrics_to_aggregate;
        (string tag, IAggrigator aggrigator)[] tagsToAggrigate;

        public MetricAggrigator(IEnumerable<string> metrics_to_aggregate, params (string tag, IAggrigator aggrigator)[] tagsToAggrigate)
        {
            this.metrics_to_aggregate = new HashSet<string>(metrics_to_aggregate);
            this.tagsToAggrigate = tagsToAggrigate;
        }

        public IEnumerable<Metric> AggrigateMetrics(IEnumerable<Metric> metrics)
        {
            foreach (var (tag, agg) in this.tagsToAggrigate)
            {
                metrics = this.AggregateTag(tag, agg, metrics);
            }

            return metrics;
        }

        IEnumerable<Metric> AggregateTag(string tag, IAggrigator aggrigator, IEnumerable<Metric> metrics)
        {
            var aggrigateValues = new Dictionary<AggrigateMetric, IAggrigator>();
            foreach (Metric metric in metrics)
            {
                if (this.metrics_to_aggregate.Contains(metric.Name) && metric.Tags.ContainsKey(tag))
                {
                    var aggrigateMetric = new AggrigateMetric(metric, tag);
                    IAggrigator agg;
                    if (!aggrigateValues.TryGetValue(aggrigateMetric, out agg))
                    {
                        agg = aggrigator.New();
                        aggrigateValues.Add(aggrigateMetric, agg);
                    }

                    agg.PutValue(metric.Value);
                }
                else
                {
                    yield return metric;
                }
            }

            foreach (var aggrigate in aggrigateValues)
            {
                yield return aggrigate.Key.ToMetric(aggrigate.Value.GetAggrigate());
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
                this.Tags = new Dictionary<string, string>(metric.Tags.Where(t => t.Key != aggrigateTag));
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
                this.hash = HashCode.Combine(this.Name.GetHashCode(), this.TimeGeneratedUtc.GetHashCode(), this.Tags.Select(o => HashCode.Combine(o.Key.GetHashCode(), o.Value.GetHashCode())).OrderBy(h => h).Aggregate(0, HashCode.Combine));

                return (int)this.hash;
            }
        }
    }
}
