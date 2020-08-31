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
        AggrigationTemplate[] metricsToAggrigate;

        public MetricAggrigator(params AggrigationTemplate[] metricsToAggrigate)
        {
            this.metricsToAggrigate = metricsToAggrigate;
        }

        // Will aggregate all metrics for all aggregtion templates
        public IEnumerable<Metric> AggrigateMetrics(IEnumerable<Metric> metrics)
        {
            // Aggregate is way overused in this class, but this Aggregate function is from linq
            return this.metricsToAggrigate.Aggregate(metrics, this.AggregateMetric);
        }

        // Will aggregate metrics for a single aggregation template
        IEnumerable<Metric> AggregateMetric(IEnumerable<Metric> metrics, AggrigationTemplate aggrigation)
        {
            return aggrigation.TagsToAggrigate.Aggregate(metrics, (m, agg) => this.AggregateTag(m, aggrigation.Name, agg.tag, agg.aggrigator));
        }

        // Will aggregate metrics for a single tag of a single template
        IEnumerable<Metric> AggregateTag(IEnumerable<Metric> metrics, string metricName, string tag, IAggrigator aggrigator)
        {
            var aggrigateValues = new DefaultDictionary<AggrigateMetric, IAggrigator>(_ => aggrigator.New());
            foreach (Metric metric in metrics)
            {
                // if metric is the aggrigation target and it has a tag that should be aggrigated
                if (metric.Name == metricName && metric.Tags.ContainsKey(tag))
                {
                    var aggrigateMetric = new AggrigateMetric(metric, tag);
                    aggrigateValues[aggrigateMetric].PutValue(metric.Value);
                }
                else
                {
                    yield return metric;
                }
            }

            // aggregate all and construct new metrics from result
            foreach (var aggrigatePair in aggrigateValues)
            {
                double aggrigatedValue = aggrigatePair.Value.GetAggrigate();
                yield return aggrigatePair.Key.ToMetric(aggrigatedValue);
            }
        }
    }
}
