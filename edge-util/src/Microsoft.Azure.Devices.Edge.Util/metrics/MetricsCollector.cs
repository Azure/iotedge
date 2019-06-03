// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics
{
    using System;
    using System.Globalization;
    using Microsoft.Azure.Devices.Edge.Util.Metrics.AppMetrics;
    using Microsoft.Azure.Devices.Edge.Util.Metrics.NullMetrics;
    using Microsoft.Extensions.Configuration;

    public class Metrics : IDisposable
    {
        const string DefaultSuffix = "metrics";
        const int DefaultPort = 80;
        const string MetricsUrlPrefixFormat = "http://*:{0}/{1}/";
        static readonly object StateLock = new object();
        static Option<MetricsListener> metricsListener = Option.None<MetricsListener>();

        public static IMetricsProvider Instance { get; private set; } = new NullMetricsProvider();

        public static void InitPrometheusMetrics(IConfiguration configuration)
        {
            bool enabled = configuration.GetValue("enabled", false);
            if (enabled)
            {
                string suffix = DefaultSuffix;
                int port = DefaultPort;
                IConfiguration listenerConfiguration = configuration.GetSection("listener");
                if (listenerConfiguration != null)
                {
                    suffix = listenerConfiguration.GetValue("suffix", DefaultSuffix);
                    port = listenerConfiguration.GetValue("port", DefaultPort);
                }

                InitPrometheusMetrics(port, suffix);
            }
        }

        public static void InitPrometheusMetrics(int port, string urlSuffix)
        {
            lock (StateLock)
            {
                if (!metricsListener.HasValue)
                {
                    Instance = MetricsProvider.Create();
                    string url = GetMetricsListenerUrlPrefix(port, urlSuffix);
                    metricsListener = Option.Some(InitMetricsListener(url, Instance));
                }
            }
        }

        public void Dispose() => metricsListener.ForEach(m => m.Dispose());

        static MetricsListener InitMetricsListener(string url, IMetricsProvider metricsProvider)
        {
            return new MetricsListener(url, metricsProvider);
        }

        static string GetMetricsListenerUrlPrefix(int port, string urlSuffix)
            => string.Format(CultureInfo.InvariantCulture, MetricsUrlPrefixFormat, port.ToString(), urlSuffix.Trim('/', ' '));
    }
}
