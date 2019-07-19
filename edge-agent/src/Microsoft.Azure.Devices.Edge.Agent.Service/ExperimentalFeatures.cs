// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Service
{
    using Microsoft.Extensions.Configuration;

    public class ExperimentalFeatures
    {
        ExperimentalFeatures(bool enabled, bool disableCloudSubscriptions, bool enableUploadLogs)
        {
            this.Enabled = enabled;
            this.DisableCloudSubscriptions = disableCloudSubscriptions;
            this.EnableUploadLogs = enableUploadLogs;
        }

        public static ExperimentalFeatures Init(IConfiguration experimentalFeaturesConfig)
        {
            bool enabled = experimentalFeaturesConfig.GetValue("enabled", false);
            bool disableCloudSubscriptions = enabled && experimentalFeaturesConfig.GetValue("disableCloudSubscriptions", false);
            bool enableUploadLogs = enabled && experimentalFeaturesConfig.GetValue("enableUploadLogs", false);
            return new ExperimentalFeatures(enabled, disableCloudSubscriptions, enableUploadLogs);
        }

        public bool Enabled { get; }

        public bool DisableCloudSubscriptions { get; }

        public bool EnableUploadLogs { get; }
    }
}
