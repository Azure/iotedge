// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics
{
    using System;
    using System.Globalization;
    using Microsoft.Azure.Devices.Edge.Util.Metrics.AppMetrics;
    using Microsoft.Azure.Devices.Edge.Util.Metrics.NullMetrics;

    public class Metrics : IDisposable
    {
        const string MetricsUrlPrefixFormat = "http://*/{0}/";
        static readonly object StateLock = new object();
        static Option<MetricsListener> metricsListener = Option.None<MetricsListener>();

        public static IMetricsProvider Instance { get; private set; } = new NullMetricsProvider();

        public static void InitPrometheusMetrics(string urlSuffix)
        {
            lock (StateLock)
            {
                if (!metricsListener.HasValue)
                {
                    Instance = MetricsProvider.Create();
                    metricsListener = Option.Some(InitMetricsListener(urlSuffix, Instance));
                }
            }
        }

        public void Dispose() => metricsListener.ForEach(m => m.Dispose());

        static MetricsListener InitMetricsListener(string urlSuffix, IMetricsProvider metricsProvider)
        {
            string metricsListenerUrlPrefix = GetMetricsListenerUrlPrefix(urlSuffix);
            return new MetricsListener(metricsListenerUrlPrefix, metricsProvider);
        }

        static string GetMetricsListenerUrlPrefix(string urlSuffix)
            => string.Format(CultureInfo.InvariantCulture, MetricsUrlPrefixFormat, urlSuffix.Trim('/', ' '));
    }
}
