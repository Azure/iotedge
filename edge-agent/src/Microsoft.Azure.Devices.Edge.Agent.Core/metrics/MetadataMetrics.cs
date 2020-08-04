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
        readonly Func<Task<SystemInfo>> getSystemMetadata;

        public MetadataMetrics(IMetricsProvider metricsProvider, Func<Task<SystemInfo>> getSystemMetadata)
        {
            this.getSystemMetadata = Preconditions.CheckNotNull(getSystemMetadata, nameof(getSystemMetadata));

            Preconditions.CheckNotNull(metricsProvider, nameof(metricsProvider));
            this.metaData = metricsProvider.CreateGauge(
                "metadata",
                "General metadata about the device. The value is always 0, information is encoded in the tags.",
                new List<string> { "edge_agent_version", "experimental_features", "host_information", MetricsConstants.MsTelemetry });
        }

        public async Task Start(ILogger logger, string agentVersion, string experimentalFeatures)
        {
            logger.LogInformation("Collecting metadata metrics");
            string edgeletVersion = Newtonsoft.Json.JsonConvert.SerializeObject(await this.getSystemMetadata());

            string[] values = { agentVersion, experimentalFeatures, edgeletVersion, true.ToString() };
            this.metaData.Set(0, values);
            logger.LogInformation($"Set metadata metrics: {values.Join(", ")}");
        }
    }
}
