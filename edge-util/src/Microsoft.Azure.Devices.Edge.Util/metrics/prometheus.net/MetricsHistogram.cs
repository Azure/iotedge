// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics.Prometheus.Net
{
    using System.Collections.Generic;
    using global::Prometheus;

    public class MetricsHistogram : BaseMetric, IMetricsHistogram
    {
        readonly Summary summary;

        public MetricsHistogram(string name, string description, List<string> labelNames, List<string> defaultLabelValues)
            : base(labelNames, defaultLabelValues)
        {
            this.summary = Metrics.CreateSummary(
                name,
                description,
                new SummaryConfiguration
                {
                    Objectives = new[]
                    {
                        new QuantileEpsilonPair(0.5, 0.05),
                        new QuantileEpsilonPair(0.9, 0.05),
                        new QuantileEpsilonPair(0.95, 0.01),
                        new QuantileEpsilonPair(0.99, 0.01),
                        new QuantileEpsilonPair(0.999, 0.01),
                        new QuantileEpsilonPair(0.9999, 0.01),
                    },
                    LabelNames = labelNames.ToArray()
                });
        }

        public void Update(double value, string[] labelValues) =>
            this.summary
                .WithLabels(this.GetLabelValues(labelValues))
                .Observe(value);
    }
}
