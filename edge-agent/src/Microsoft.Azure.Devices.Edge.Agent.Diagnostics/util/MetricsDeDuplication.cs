// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Diagnostics.Util
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    public static class MetricsDeDuplication
    {
        public static IEnumerable<Metric> RemoveDuplicateMetrics(IEnumerable<Metric> metrics)
        {
            Dictionary<int, (Metric metric, bool)> previousValues = new Dictionary<int, (Metric, bool)>();

            foreach (Metric newMetric in metrics)
            {
                int key = newMetric.GetMetricKey();
                (Metric metric, bool isFirst) previous;
                if (previousValues.TryGetValue(key, out previous))
                {
                    if (previous.metric.Value != newMetric.Value)
                    {
                        // Metric value changed, return old value
                        yield return previous.metric;
                        previousValues[key] = (newMetric, true);
                    }
                    else
                    {
                        // If the old value was the first point at that value, make a baseline point for that value
                        if (previous.isFirst)
                        {
                            yield return previous.metric;
                        }

                        // Since the previous value was between the baseline point and the new point, it can be dropped without losing information
                        previousValues[key] = (newMetric, false);
                    }
                }
                else
                {
                    // First time encountering this metric
                    previousValues[key] = (newMetric, true);
                }
            }

            // return the ending value for all metrics
            foreach (var remainingMetric in previousValues.Values)
            {
                yield return remainingMetric.metric;
            }
        }
    }
}
