// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using System.Threading.Tasks;
    using App.Metrics;
    using App.Metrics.Counter;
    using App.Metrics.Scheduling;
    using App.Metrics.Timer;

    public static class Metrics
    {
        static Metrics()
        {
            MetricsCollector = Option.None<IMetricsRoot>();
        }

        class NullDisposable : IDisposable
        {
            public void Dispose()
            {
            }
        }

        public static Option<IMetricsRoot> MetricsCollector { set; get; }

        public static void StartReporting()
        {
            // Start reporting metrics every 20s
            MetricsCollector.Match(m =>
            {
                var scheduler = new AppMetricsTaskScheduler(
                    TimeSpan.FromSeconds(20),
                    async () =>
                    {
                        await Task.WhenAll(m.ReportRunner.RunAllAsync());
                    });
                scheduler.Start();
                return m;
            }, () => throw new InvalidOperationException("Uninitialized metrics root"));
        }

        public static void Count(MetricTags tags, CounterOptions options)
        {
            Preconditions.CheckNotNull(tags);
            Preconditions.CheckNotNull(options);

            MetricsCollector.ForEach(mroot =>
            {
                mroot.Measure.Counter.Increment(options, tags);
            });
        }

        public static IDisposable Latency(MetricTags tags, TimerOptions options)
        {
            Preconditions.CheckNotNull(tags);
            Preconditions.CheckNotNull(options);

            return MetricsCollector.Map(mroot => mroot.Measure.Timer.Time(options, tags) as IDisposable).GetOrElse(() => new NullDisposable());
        }
    }
}
