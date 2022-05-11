// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics
{
    using System;
    using Microsoft.Extensions.Configuration;

    public class MetricsConfig
    {
        public MetricsConfig(IConfiguration config, bool enabledDefault = true)
        {
            this.Enabled = config.GetValue("MetricsEnabled", enabledDefault);
            this.HistogramMaxAge = config.GetValue("MetricsHistogramMaxAge", TimeSpan.FromHours(1));

            this.ListenerConfig = MetricsListenerConfig.Create(config);
        }

        public bool Enabled { get; }

        public MetricsListenerConfig ListenerConfig { get; }

        public TimeSpan HistogramMaxAge { get; }
    }
}
