// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core.Metrics
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Metrics;
    using Microsoft.Extensions.Logging;

    public class MetadataMetrics
    {
        readonly IMetricsGauge metaData;

        public MetadataMetrics(IMetricsProvider metricsProvider)
        {
            Preconditions.CheckNotNull(metricsProvider, nameof(metricsProvider));
            this.metaData = metricsProvider.CreateGauge(
                "metadata",
                "General metadata about the device. The value is always 0, information is encoded in the tags.",
                new List<string> { "edge_agent_version", MetricsConstants.MsTelemetry });
        }

        public async Task Start(ILogger logger, string version)
        {
            logger.LogInformation("Collecting metadata metrics");
            await Task.Yield();

            string[] values = { version, true.ToString() };
            this.metaData.Set(0, values);
            logger.LogInformation($"Set metadata metrics: {values.Join(", ")}");
        }
    }
}
