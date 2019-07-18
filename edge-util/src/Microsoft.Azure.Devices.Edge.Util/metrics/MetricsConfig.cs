// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics
{
    using Microsoft.Extensions.Configuration;

    public class MetricsConfig
    {
        public MetricsConfig(bool enabled, MetricsListenerConfig listenerConfig)
        {
            this.Enabled = enabled;
            this.ListenerConfig = listenerConfig;
        }

        public static MetricsConfig Create(IConfiguration config)
        {
            bool enabled = config.GetValue("enabled", false);
            MetricsListenerConfig listenerConfig = enabled
                ? MetricsListenerConfig.Create(config.GetSection("listener"))
                : new MetricsListenerConfig();
            return new MetricsConfig(enabled, listenerConfig);
        }

        public bool Enabled { get; }

        public MetricsListenerConfig ListenerConfig { get; }
    }
}
