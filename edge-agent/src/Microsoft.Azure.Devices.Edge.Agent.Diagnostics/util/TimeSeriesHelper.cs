// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Diagnostics.Util
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    public static class TimeSeriesHelper
    {
        /// <summary>
        /// Reduces the points in a timeseries. This removes extra points between metrics of identical value.
        /// If there are three or more metrics in a row with the same value, this removes all points in the middle, keeping
        /// only the first and last points.
        /// </summary>
        /// <param name="metrics">Points to reduce. Must be in chronological order.</param>
        /// <returns>Reduced list of points with the same meaning. If graphed, the lines should look identical.</returns>
        public static IEnumerable<Metric> CondenseTimeSeries(this IEnumerable<Metric> metrics)
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
