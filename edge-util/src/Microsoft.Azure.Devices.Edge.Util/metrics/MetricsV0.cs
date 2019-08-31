// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using App.Metrics;
    using App.Metrics.Counter;
    using App.Metrics.Formatters.Json;
    using App.Metrics.Gauge;
    using App.Metrics.Scheduling;
    using App.Metrics.Timer;
    using Microsoft.Extensions.Configuration;

    public static class MetricsV0
    {
        static readonly object gaugeListLock;

        static MetricsV0()
        {
            MetricsCollector = Option.None<IMetricsRoot>();
            Gauges = new List<Action>();
            gaugeListLock = new object();
        }

        public static Option<IMetricsRoot> MetricsCollector { get; private set; }

        static List<Action> Gauges { get; set; }

        public static void BuildMetricsCollector(IConfigurationRoot configuration)
        {
            bool collectMetrics = configuration.GetValue("CollectMetrics", false);

            if (collectMetrics)
            {
                IConfiguration metricsConfigurationSection = configuration.GetSection("Metrics");
                string metricsStoreType = metricsConfigurationSection.GetValue<string>("MetricsStoreType");

                if (metricsStoreType == "influxdb")
                {
                    string metricsDbName = metricsConfigurationSection.GetValue("MetricsDbName", "metricsdatabase");
                    string influxDbUrl = metricsConfigurationSection.GetValue("InfluxDbUrl", "http://influxdb:8086");
                    IMetricsRoot metricsCollector = new MetricsBuilder()
                        .Report.ToInfluxDb(
                            options =>
                            {
                                options.InfluxDb.BaseUri = new Uri(influxDbUrl);
                                options.InfluxDb.Database = metricsDbName;
                                options.InfluxDb.CreateDataBaseIfNotExists = true;
                                options.FlushInterval = TimeSpan.FromSeconds(10);
                            }).Build();
                    MetricsCollector = Option.Some(metricsCollector);
                    StartReporting(metricsCollector);
                }
                else
                {
                    string metricsStoreLocation = metricsConfigurationSection.GetValue("MetricsStoreLocation", "metrics");
                    bool appendToMetricsFile = metricsConfigurationSection.GetValue("MetricsStoreAppend", false);
                    IMetricsRoot metricsCollector = new MetricsBuilder()
                        .Report.ToTextFile(
                            options =>
                            {
                                options.MetricsOutputFormatter = new MetricsJsonOutputFormatter();
                                options.AppendMetricsToTextFile = appendToMetricsFile;
                                options.FlushInterval = TimeSpan.FromSeconds(20);
                                options.OutputPathAndFileName = metricsStoreLocation;
                            }).Build();
                    MetricsCollector = Option.Some(metricsCollector);
                    StartReporting(metricsCollector);
                }
            }
        }

        public static void RegisterGaugeCallback(Action callback)
        {
            lock (gaugeListLock)
            {
                Gauges.Add(callback);
            }
        }

        public static void CountIncrement(MetricTags tags, CounterOptions options, long amount)
        {
            Preconditions.CheckNotNull(tags);
            Preconditions.CheckNotNull(options);
            Preconditions.CheckNotNull(amount);

            MetricsCollector.ForEach(mroot => { mroot.Measure.Counter.Increment(options, tags, amount); });
        }

        public static void CountDecrement(MetricTags tags, CounterOptions options, long amount)
        {
            Preconditions.CheckNotNull(tags);
            Preconditions.CheckNotNull(options);
            Preconditions.CheckNotNull(amount);

            MetricsCollector.ForEach(mroot => { mroot.Measure.Counter.Decrement(options, tags, amount); });
        }

        public static void CountIncrement(CounterOptions options, long amount)
        {
            Preconditions.CheckNotNull(options);
            Preconditions.CheckNotNull(amount);

            MetricsCollector.ForEach(mroot => { mroot.Measure.Counter.Increment(options, amount); });
        }

        public static void CountDecrement(CounterOptions options, long amount)
        {
            Preconditions.CheckNotNull(options);
            Preconditions.CheckNotNull(amount);

            MetricsCollector.ForEach(mroot => { mroot.Measure.Counter.Decrement(options, amount); });
        }

        public static IDisposable Latency(MetricTags tags, TimerOptions options)
        {
            Preconditions.CheckNotNull(tags);
            Preconditions.CheckNotNull(options);

            return MetricsCollector.Map(mroot => mroot.Measure.Timer.Time(options, tags) as IDisposable).GetOrElse(() => new NullDisposable());
        }

        public static void SetGauge(GaugeOptions options, long amount)
        {
            Preconditions.CheckNotNull(options);
            Preconditions.CheckNotNull(amount);

            MetricsCollector.ForEach(mroot => { mroot.Measure.Gauge.SetValue(options, amount); });
        }

        static void StartReporting(IMetricsRoot metricsCollector)
        {
            // Start reporting metrics every 5s
            var scheduler = new AppMetricsTaskScheduler(
                TimeSpan.FromSeconds(5),
                async () =>
                {
                    foreach (var callback in Gauges)
                    {
                        callback();
                    }

                    await Task.WhenAll(metricsCollector.ReportRunner.RunAllAsync());
                });
            scheduler.Start();
        }

        class NullDisposable : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
