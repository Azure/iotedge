// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics
{
    using Microsoft.Azure.Devices.Edge.Util.Metrics.NullMetrics;
    using Microsoft.Extensions.Logging;

    public static class Metrics
    {
        static readonly object StateLock = new object();

        public static IMetricsProvider Instance { get; private set; } = new NullMetricsProvider();

        public static IMetricsListener Listener { get; private set; } = new NullMetricsListener();

        public static void Init(IMetricsProvider metricsProvider, IMetricsListener metricsListener, ILogger logger)
        {
            Preconditions.CheckNotNull(metricsProvider, nameof(metricsProvider));
            Preconditions.CheckNotNull(metricsListener, nameof(metricsListener));

            lock (StateLock)
            {
                Instance = metricsProvider;
                Listener = metricsListener;
                Listener.Start(logger);
            }
        }
    }
}
