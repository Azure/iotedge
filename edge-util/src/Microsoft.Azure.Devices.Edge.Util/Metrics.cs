// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using System.Threading.Tasks;
    using App.Metrics;
    using App.Metrics.Counter;
    using App.Metrics.Formatters.Json;
    using App.Metrics.Scheduling;
    using App.Metrics.Timer;
    using Microsoft.Extensions.Configuration;

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

        public static Option<IMetricsRoot> MetricsCollector { private set; get; }

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
                    string influxDbUrl = metricsConfigurationSection.GetValue("InfluxDbUrl", "http://127.0.0.1:8086");
                    IMetricsRoot metricsCollector = new MetricsBuilder()
                        .Report.ToInfluxDb(
                            options =>
                            {
                                options.InfluxDb.BaseUri = new Uri(influxDbUrl);
                                options.InfluxDb.Database = metricsDbName;
                                options.InfluxDb.CreateDataBaseIfNotExists = true;
                                options.FlushInterval = TimeSpan.FromSeconds(10);
                            }
                        ).Build();
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
                            }
                        ).Build();
                    MetricsCollector = Option.Some(metricsCollector);
                    StartReporting(metricsCollector);
                }
            }
        }

        static void StartReporting(IMetricsRoot metricsCollector)
        {
            // Start reporting metrics every 20s
            var scheduler = new AppMetricsTaskScheduler(
                    TimeSpan.FromSeconds(20),
                    async () =>
                    {
                        await Task.WhenAll(metricsCollector.ReportRunner.RunAllAsync());
                    });
            scheduler.Start();
        }

        public static void Count(MetricTags tags, CounterOptions options)
        {
            Preconditions.CheckNotNull(tags);
            Preconditions.CheckNotNull(options);

            MetricsCollector.ForEach(mroot =>
            {
                mroot.Measure.Counter.Increment(options, tags);
                mroot.Provider.Counter.Instance(options).Reset();
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
