// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Device
{
    using Microsoft.Azure.Devices.Edge.Util.Metrics;
    using System.Collections.Generic;

    public class DeviceConnectionMetrics
    {
        readonly IMetricsCounter authCounter;

        DeviceConnectionMetrics()
        {
            this.authCounter = Util.Metrics.Metrics.Instance.CreateCounter(
                "client_connect_failed",
                "Client connection failure",
                new List<string> { "id", "reason", "protocol" });
        }

        public static DeviceConnectionMetrics Instance { get; } = new DeviceConnectionMetrics();

        public void LogAuthenticationFailure(long metricValue, string id, string reason, string protocol)
        {
            this.authCounter.Increment(metricValue, new[] { id, reason, protocol });
        }
    }
}
