// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics
{
    public class MetricsConfig
    {
        public MetricsConfig(bool enabled, MetricsListenerConfig listenerConfig)
        {
            this.Enabled = enabled;
            this.ListenerConfig = listenerConfig;
        }

        public bool Enabled { get; }

        public MetricsListenerConfig ListenerConfig { get; }
    }
}
