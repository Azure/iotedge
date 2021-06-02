// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics.Prometheus.Net
{
    using System;
    using System.Collections.Generic;
    using global::Prometheus;

    public class MetricsTimer : BaseMetric, IMetricsTimer
    {
        readonly Summary summary;

        public MetricsTimer(string name, string description, List<string> labelNames, List<string> defaultLabelValues)
            : base(labelNames, defaultLabelValues)
        {
            this.summary = Metrics.CreateSummary(
                name,
                description,
                new SummaryConfiguration
                {
                    Objectives = new[]
                    {
                        new QuantileEpsilonPair(0.1, 0.01),
                        new QuantileEpsilonPair(0.5, 0.01),
                        new QuantileEpsilonPair(0.9, 0.01),
                        new QuantileEpsilonPair(0.99, 0.01),
                    },
                    LabelNames = labelNames.ToArray()
                });
        }

        public IDisposable GetTimer(string[] labelValues) => this.summary.WithLabels(this.GetLabelValues(labelValues)).NewTimer();
    }
}
