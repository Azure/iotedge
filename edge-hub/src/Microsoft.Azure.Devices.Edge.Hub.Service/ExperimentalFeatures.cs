// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service
{
    using Microsoft.Extensions.Configuration;

    public class ExperimentalFeatures
    {
        ExperimentalFeatures(bool enabled, bool disableCloudSubscriptions, bool disableConnectivityCheck)
        {
            this.Enabled = enabled;
            this.DisableCloudSubscriptions = disableCloudSubscriptions;
            this.DisableConnectivityCheck = disableConnectivityCheck;
        }

        public static ExperimentalFeatures Init(IConfiguration experimentalFeaturesConfig)
        {
            bool enabled = experimentalFeaturesConfig.GetValue("Enabled", false);
            bool disableCloudSubscriptions = enabled && experimentalFeaturesConfig.GetValue("DisableCloudSubscriptions", false);
            bool disableConnectivityCheck = enabled && experimentalFeaturesConfig.GetValue("DisableConnectivityCheck", false);
            return new ExperimentalFeatures(enabled, disableCloudSubscriptions, disableConnectivityCheck);
        }

        public bool Enabled { get; }

        public bool DisableCloudSubscriptions { get; }

        public bool DisableConnectivityCheck { get; }
    }
}
