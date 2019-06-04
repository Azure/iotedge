// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics.AppMetrics
{
    using System;
    using System.Collections.Generic;
    using App.Metrics;
    using App.Metrics.Timer;

    public class MetricsTimer : IMetricsTimer
    {
        readonly IMeasureTimerMetrics timerMetrics;
        readonly TimerOptions timerOptions;

        public MetricsTimer(string name, IMeasureTimerMetrics timerMetrics, Dictionary<string, string> defaultTags)
        {
            this.timerMetrics = timerMetrics;
            MetricTags defaultMetricsTags = MetricTags.Concat(MetricTags.Empty, defaultTags);
            this.timerOptions = new TimerOptions
            {
                Name = name,
                MeasurementUnit = Unit.Items,
                Tags = defaultMetricsTags,
                DurationUnit = TimeUnit.Milliseconds,
                RateUnit = TimeUnit.Milliseconds
            };
        }

        public IDisposable GetTimer()
        {
            return this.timerMetrics.Time(this.timerOptions);
        }

        public IDisposable GetTimer(Dictionary<string, string> tags)
        {
            return this.timerMetrics.Time(this.timerOptions, MetricTags.Concat(MetricTags.Empty, tags));
        }
    }
}
