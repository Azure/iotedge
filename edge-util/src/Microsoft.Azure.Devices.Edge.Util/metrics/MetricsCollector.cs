// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics
{
    using System;
    using System.Globalization;
    using Microsoft.Azure.Devices.Edge.Util.Metrics.AppMetrics;
    using Microsoft.Azure.Devices.Edge.Util.Metrics.NullMetrics;

    public class Metrics : IDisposable
    {
        const string MetricsUrlPrefixFormat = "http://localhost/{0}/";
        static readonly object StateLock = new object();
        static Option<MetricsListener> metricsListener = Option.None<MetricsListener>();

        public static IMetricsProvider Instance { get; private set; } = new NullMetricsProvider();

        public static void InitPrometheusMetrics(string url)
        {
            lock (StateLock)
            {
                if (!metricsListener.HasValue)
                {
                    Instance = MetricsProvider.Create();
                    metricsListener = Option.Some(InitMetricsListener(url, Instance));
                }
            }
        }

        public void Dispose() => metricsListener.ForEach(m => m.Dispose());

        static MetricsListener InitMetricsListener(string url, IMetricsProvider metricsProvider)
        {
            return new MetricsListener(url, metricsProvider);
        }

        static string GetMetricsListenerUrlPrefix(string urlSuffix)
            => string.Format(CultureInfo.InvariantCulture, MetricsUrlPrefixFormat, urlSuffix.Trim('/', ' '));
    }
}
