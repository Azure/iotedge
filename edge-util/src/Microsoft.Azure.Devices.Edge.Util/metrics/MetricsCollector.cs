// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics
{
    using System;
    using Microsoft.Azure.Devices.Edge.Util.Metrics.Prometheus.Net;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    public class MetricsCollector : IDisposable
    {
        const string DefaultHost = "*";
        const int DefaultPort = 80;
        const string DefaultSuffix = "metrics";
        static readonly object StateLock = new object();
        static Option<IMetricsListener> metricsListener = Option.None<IMetricsListener>();

        public static IMetricsProvider Instance { get; private set; }

        public static void InitMetricsListener(IConfiguration configuration, string iotHubName, string deviceId, ILogger logger)
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

                InitMetricsListener(iotHubName, deviceId, host, port, suffix, logger);
            }
        }

        public static void InitMetricsListener(string iotHubName, string deviceId, string host, int port, string suffix, ILogger logger)
        {
            lock (StateLock)
            {
                if (!metricsListener.HasValue)
                {
                    Instance = new MetricsProvider(MetricsConstants.EdgeHubLabel, iotHubName, deviceId);
                    metricsListener = Option.Some(new Prometheus.Net.MetricsListener(host, port, suffix, logger) as IMetricsListener);
                }
            }
        }

        public void Dispose() => metricsListener.ForEach(m => m.Dispose());
    }
}
