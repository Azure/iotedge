// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Service
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    public class KubernetesExperimentalFeatures
    {
        const string SectionName = "ExperimentalFeatures";
        const string EnabledKey = "enabled";
        const string EnableK8SExtentionsKey = "enableK8SExtensions";

        KubernetesExperimentalFeatures(bool enabled, bool enableExtensions)
        {
            this.Enabled = enabled;
            this.EnableExtensions = enableExtensions;
        }

        public static KubernetesExperimentalFeatures Create(IConfiguration experimentalFeaturesConfig, ILogger logger)
        {
            bool enabled = experimentalFeaturesConfig.GetValue(EnabledKey, false);
            bool enableK8SExtensions = enabled && experimentalFeaturesConfig.GetValue(EnableK8SExtentionsKey, false);
            var experimentalFeatures = new KubernetesExperimentalFeatures(enabled, enableK8SExtensions);
            logger.LogInformation($"Experimental features configuration: {experimentalFeatures.ToJson()}");
            return experimentalFeatures;
        }

        public bool Enabled { get; }

        public bool EnableExtensions { get; }

        public IDictionary<string, bool> GetEnvVars() => new Dictionary<string, bool>
            {
                [$"{SectionName}__{EnabledKey}"] = this.Enabled,
                [$"{SectionName}__{EnableK8SExtentionsKey}"] = this.EnableExtensions
            };
    }
}
