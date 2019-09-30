// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Service
{
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment.Deployment;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    public class KubernetesExperimentalFeatures
    {
        KubernetesExperimentalFeatures(bool enabled, bool enableVolumes, bool enableResources, bool enableNodeSelector)
        {
            this.Enabled = enabled;
            this.EnableVolumes = enableVolumes;
            this.EnableResources = enableResources;
            this.EnableNodeSelector = enableNodeSelector;
        }

        public static KubernetesExperimentalFeatures Create(IConfiguration experimentalFeaturesConfig, ILogger logger)
        {
            bool enabled = experimentalFeaturesConfig.GetValue("enabled", false);
            bool enableVolumes = enabled && experimentalFeaturesConfig.GetValue("k8sEnableVolumes", false);
            bool enableResources = enabled && experimentalFeaturesConfig.GetValue("k8sEnableResources", false);
            bool enableNodeSelector = enabled && experimentalFeaturesConfig.GetValue("k8sEnableNodeSelector", false);
            var experimentalFeatures = new KubernetesExperimentalFeatures(enabled, enableVolumes, enableResources, enableNodeSelector);
            logger.LogInformation($"Experimental features configuration: {experimentalFeatures.ToJson()}");
            return experimentalFeatures;
        }

        public bool Enabled { get; }

        public bool EnableVolumes { get; }

        public bool EnableResources { get; }

        public bool EnableNodeSelector { get; }
    }
}
