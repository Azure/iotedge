// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Diagnostics.Util.Aggregation
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
    public class MetricAggregator
    {
        AggregationTemplate[] metricsToAggrigate;

        public MetricAggregator(params AggregationTemplate[] metricsToAggrigate)
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
        IEnumerable<Metric> AggregateMetric(IEnumerable<Metric> metrics, AggregationTemplate aggregation)
        {
            return aggregation.TagsToAggrigate.Aggregate(metrics, (m, tagAggregation) => this.AggregateTag(m, aggregation.TargetMetricNames, tagAggregation.targetTag, tagAggregation.aggregator));
        }

        // Will aggregate metrics for a single tag of a single template
        IEnumerable<Metric> AggregateTag(IEnumerable<Metric> metrics, IEnumerable<string> targetMetricNames, string targetTag, IAggregator aggregator)
        {
            var aggrigateValues = new DefaultDictionary<AggrigateMetric, IAggregator>(_ => aggregator.New());
            foreach (Metric metric in metrics)
            {
                // if metric is the aggregation target and it has a tag that should be aggrigated
                if (targetMetricNames.Contains(metric.Name) && metric.Tags.ContainsKey(targetTag))
                {
                    var aggrigateMetric = new AggrigateMetric(metric, targetTag);
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
