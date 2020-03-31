// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics
{
    using Microsoft.Extensions.Configuration;

    public class MetricsConfig
    {
        public MetricsConfig(IConfiguration config)
        {
            this.Enabled = config.GetValue("MetricsEnabled", true);
            this.ListenerConfig = MetricsListenerConfig.Create(config);
        }

        public bool Enabled { get; }

        public MetricsListenerConfig ListenerConfig { get; }
    }
}
