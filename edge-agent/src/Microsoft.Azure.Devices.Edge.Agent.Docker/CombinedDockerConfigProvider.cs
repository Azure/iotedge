// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Docker
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using global::Docker.DotNet.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;

    /// <summary>
    /// This implementation combines docker image and docker create
    /// options from the module, and the registry credentials from the runtime info or environment
    /// and returns them.
    /// </summary>
    public class CombinedDockerConfigProvider : ICombinedConfigProvider<CombinedDockerConfig>
    {
        readonly IEnumerable<AuthConfig> authConfigs;

        public CombinedDockerConfigProvider(IEnumerable<AuthConfig> authConfigs)
        {
            this.authConfigs = Preconditions.CheckNotNull(authConfigs, nameof(authConfigs));
        }

        public virtual CombinedDockerConfig GetCombinedConfig(IModule module, IRuntimeInfo runtimeInfo)
        {
            if (!(module is IModule<DockerConfig> moduleWithDockerConfig))
            {
                throw new InvalidOperationException("Module does not contain DockerConfig");
            }

            if (!(runtimeInfo is IRuntimeInfo<DockerRuntimeConfig> dockerRuntimeConfig))
            {
                throw new InvalidOperationException("RuntimeInfo does not contain DockerRuntimeConfig");
            }

            // Convert registry credentials from config to AuthConfig objects
            List<AuthConfig> deploymentAuthConfigs = dockerRuntimeConfig.Config.RegistryCredentials
                .Select(c => new AuthConfig { ServerAddress = c.Value.Address, Username = c.Value.Username, Password = c.Value.Password })
                .ToList();

            // First try to get matching auth config from the runtime info. If no match is found,
            // then try the auth configs from the environment
            Option<AuthConfig> authConfig = deploymentAuthConfigs.FirstAuthConfig(moduleWithDockerConfig.Config.Image)
                .Else(() => this.authConfigs.FirstAuthConfig(moduleWithDockerConfig.Config.Image));

            return new CombinedDockerConfig(moduleWithDockerConfig.Config.Image, moduleWithDockerConfig.Config.CreateOptions, authConfig);
        }
    }
}
