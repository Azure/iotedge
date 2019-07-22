// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Service
{
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    public class ExperimentalFeatures
    {
        ExperimentalFeatures(bool enabled, bool disableCloudSubscriptions, bool enableUploadLogs)
        {
            this.Enabled = enabled;
            this.DisableCloudSubscriptions = disableCloudSubscriptions;
            this.EnableUploadLogs = enableUploadLogs;
        }

        public static ExperimentalFeatures Init(IConfiguration experimentalFeaturesConfig, ILogger logger)
        {
            bool enabled = experimentalFeaturesConfig.GetValue("enabled", false);
            bool disableCloudSubscriptions = enabled && experimentalFeaturesConfig.GetValue("disableCloudSubscriptions", false);
            bool enableUploadLogs = enabled && experimentalFeaturesConfig.GetValue("enableUploadLogs", false);
            var experimentalFeatures = new ExperimentalFeatures(enabled, disableCloudSubscriptions, enableUploadLogs);
            logger.LogInformation($"Experimental features configuration: {experimentalFeatures.ToJson()}");
            return experimentalFeatures;
        }

        public bool Enabled { get; }

        public bool DisableCloudSubscriptions { get; }

        public bool EnableUploadLogs { get; }
    }
}
