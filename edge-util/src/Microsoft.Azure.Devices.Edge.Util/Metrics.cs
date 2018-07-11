// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using App.Metrics;
    using App.Metrics.Counter;
    using App.Metrics.Timer;

    public static class Metrics
    {
        class Disposable : IDisposable
        {
            public void Dispose()
            {
            }
        }

        public static void Count(Option<IMetricsRoot> metricsCollector, MetricTags tags, CounterOptions options)
        {
            Preconditions.CheckNotNull(metricsCollector);
            Preconditions.CheckNotNull(tags);
            Preconditions.CheckNotNull(options);

            metricsCollector.ForEach(mroot =>
            {
                mroot.Measure.Counter.Increment(options, tags);
            });
        }

        public static IDisposable Latency(Option<IMetricsRoot> metricsCollector, MetricTags tags, TimerOptions options)
        {
            Preconditions.CheckNotNull(metricsCollector);
            Preconditions.CheckNotNull(tags);
            Preconditions.CheckNotNull(options);

            return metricsCollector.Map(mroot => mroot.Measure.Timer.Time(options, tags) as IDisposable).GetOrElse(() => new Disposable());
        }
    }
}
