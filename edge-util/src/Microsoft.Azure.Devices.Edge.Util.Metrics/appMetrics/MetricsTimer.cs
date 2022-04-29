// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics.AppMetrics
{
    using System;
    using System.Collections.Generic;
    using App.Metrics;
    using App.Metrics.Timer;

    public class MetricsTimer : BaseMetric, IMetricsTimer
    {
        readonly IMeasureTimerMetrics timerMetrics;
        readonly TimerOptions timerOptions;

        public MetricsTimer(string name, IMeasureTimerMetrics timerMetrics, List<string> labelNames)
            : base(labelNames, new List<string>())
        {
            this.timerMetrics = timerMetrics;
            this.timerOptions = new TimerOptions
            {
                Name = name,
                MeasurementUnit = Unit.Items,
                DurationUnit = TimeUnit.Milliseconds,
                RateUnit = TimeUnit.Milliseconds
            };
        }

        public IDisposable GetTimer(string[] labelValues)
        {
            var tags = new MetricTags(this.LabelNames, labelValues);
            return this.timerMetrics.Time(this.timerOptions, tags);
        }
    }
}
