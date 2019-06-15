// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics
{
    using Microsoft.Extensions.Configuration;

    public class MetricsConfig
    {
        public MetricsConfig(bool enabled, Option<MetricsListenerConfig> listenerConfig)
        {
            this.Enabled = enabled;
            this.ListenerConfig = listenerConfig;
        }

        public static MetricsConfig Create(IConfiguration config)
        {
            bool enabled = config.GetValue("enabled", false);
            Option<MetricsListenerConfig> listenerConfig = enabled
                ? Option.Some(MetricsListenerConfig.Create(config))
                : Option.None<MetricsListenerConfig>();
            return new MetricsConfig(enabled, listenerConfig);
        }

        public bool Enabled { get; }

        public Option<MetricsListenerConfig> ListenerConfig { get; }
    }
}
