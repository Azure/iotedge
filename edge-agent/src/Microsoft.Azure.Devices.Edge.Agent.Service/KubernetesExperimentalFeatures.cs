// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Service
{
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    public class KubernetesExperimentalFeatures
    {
        KubernetesExperimentalFeatures(bool enabled, bool enableK8SExtensions)
        {
            this.Enabled = enabled;
            this.EnableK8SExtensions = enableK8SExtensions;
        }

        public static KubernetesExperimentalFeatures Create(IConfiguration experimentalFeaturesConfig, ILogger logger)
        {
            bool enabled = experimentalFeaturesConfig.GetValue("enabled", false);
            bool enableK8SExtensions = enabled && experimentalFeaturesConfig.GetValue("enableK8SExtensions", false);
            var experimentalFeatures = new KubernetesExperimentalFeatures(enabled, enableK8SExtensions);
            logger.LogInformation($"Experimental features configuration: {experimentalFeatures.ToJson()}");
            return experimentalFeatures;
        }

        public bool Enabled { get; }

        public bool EnableK8SExtensions { get; }
    }
}
