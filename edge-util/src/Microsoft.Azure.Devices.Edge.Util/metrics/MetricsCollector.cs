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
        const string DefaultHost = "*";
        const int DefaultPort = 80;
        const string DefaultSuffix = "metrics";
        const string MetricsUrlPrefixFormat = "http://{0}:{1}/{2}/";
        static readonly object StateLock = new object();
        static Option<MetricsListener> metricsListener = Option.None<MetricsListener>();

        public static IMetricsProvider Instance { get; private set; } = new NullMetricsProvider();

        public static void InitMetricsListener(IConfiguration configuration, string deviceId)
        {
            bool enabled = configuration.GetValue("enabled", false);
            if (enabled)
            {
                string suffix = DefaultSuffix;
                string host = DefaultHost;
                int port = DefaultPort;
                IConfiguration listenerConfiguration = configuration.GetSection("listener");
                if (listenerConfiguration != null)
                {
                    suffix = listenerConfiguration.GetValue("suffix", DefaultSuffix);
                    port = listenerConfiguration.GetValue("port", DefaultPort);
                    host = listenerConfiguration.GetValue("host", DefaultHost);
                }

                string url = GetMetricsListenerUrlPrefix(host, port, suffix);
                InitMetricsListener(url, deviceId);
            }
        }

        public static void InitMetricsListener(string prefixUrl, string deviceId)
        {
            lock (StateLock)
            {
                if (!metricsListener.HasValue)
                {
                    Instance = MetricsProvider.Create(deviceId);
                    metricsListener = Option.Some(new MetricsListener(prefixUrl, Instance));
                }
            }
        }

        public void Dispose() => metricsListener.ForEach(m => m.Dispose());

        static string GetMetricsListenerUrlPrefix(string host, int port, string urlSuffix)
            => string.Format(CultureInfo.InvariantCulture, MetricsUrlPrefixFormat, host, port.ToString(), urlSuffix.Trim('/', ' '));
    }
}
