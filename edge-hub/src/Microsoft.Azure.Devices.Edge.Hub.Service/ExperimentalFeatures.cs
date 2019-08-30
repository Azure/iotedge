// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service
{
    using Microsoft.Extensions.Configuration;

    public class ExperimentalFeatures
    {
        public ExperimentalFeatures(bool enabled, bool disableCloudSubscriptions, bool disableConnectivityCheck, bool enableMetrics)
        {
            this.Enabled = enabled;
            this.DisableCloudSubscriptions = disableCloudSubscriptions;
            this.DisableConnectivityCheck = disableConnectivityCheck;
            this.EnableMetrics = enableMetrics;
        }

        public static ExperimentalFeatures Create(IConfiguration experimentalFeaturesConfig)
        {
            bool enabled = experimentalFeaturesConfig.GetValue("enabled", false);
            bool disableCloudSubscriptions = enabled && experimentalFeaturesConfig.GetValue("disableCloudSubscriptions", false);
            bool disableConnectivityCheck = enabled && experimentalFeaturesConfig.GetValue("disableConnectivityCheck", false);
            bool enableMetrics = enabled && experimentalFeaturesConfig.GetValue("enableMetrics", false);
            return new ExperimentalFeatures(enabled, disableCloudSubscriptions, disableConnectivityCheck, enableMetrics);
        }

        public bool Enabled { get; }

        public bool DisableCloudSubscriptions { get; }

        public bool DisableConnectivityCheck { get; }

        public bool EnableMetrics { get; }
    }
}
