// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Device
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Util.Metrics;

    public class DeviceConnectionMetrics
    {
        readonly IMetricsCounter authCounter;

        DeviceConnectionMetrics()
        {
            this.authCounter = Metrics.Instance.CreateCounter(
                "client_connect_failed",
                "Client connection failure",
                new List<string> { "id", "reason", "protocol", MetricsConstants.MsTelemetry });
        }

        public static DeviceConnectionMetrics Instance { get; } = new DeviceConnectionMetrics();

        public void LogAuthenticationFailure(long metricValue, string id, string reason, string protocol)
        {
            this.authCounter.Increment(metricValue, new[] { id, reason, protocol, bool.TrueString });
        }
    }
}
