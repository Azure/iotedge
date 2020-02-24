// Copyright (c) Microsoft. All rights reserved.

namespace MetricsValidator.Util
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Microsoft.Azure.Devices.Edge.Agent.Diagnostics;
    using Microsoft.Azure.Devices.Edge.Util;

    public class HistogramQuartiles
    {
        public static readonly string[] QuartileNames = { ".5", ".9", ".95", ".99", ".999", ".9999" };

        public double Sum;
        public double Count;
        public Dictionary<string, double> Quartiles;

        public static Option<HistogramQuartiles> CreateFromMetrics(IEnumerable<Metric> metrics, string metricName)
        {
            var releventMetrics = metrics.Where(m => m.Name.Contains(metricName) && m.Tags["id"].Contains("MetricsValidator")).ToList();

            if (releventMetrics.Count != 8)
            {
                return Option.None<HistogramQuartiles>();
            }

            try
            {
                return Option.Some(new HistogramQuartiles
                {
                    Quartiles = releventMetrics.Where(m => m.Name == metricName).ToDictionary(m => m.Tags["quantile"], m => m.Value),
                    Count = releventMetrics.First(m => m.Name == $"{metricName}_count").Value,
                    Sum = releventMetrics.First(m => m.Name == $"{metricName}_sum").Value,
                });
            }
            catch
            {
                return Option.None<HistogramQuartiles>();
            }
        }
    }
}
