// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Edgelet.Docker
{
    using System;
    using System.Collections.Generic;
    using global::Docker.DotNet.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    public class CombinedEdgeletConfigProvider : CombinedDockerConfigProvider
    {
        readonly IConfigSource configSource;

        public CombinedEdgeletConfigProvider(IEnumerable<AuthConfig> authConfigs,
            IConfigSource configSource)
            : base(authConfigs)
        {
            this.configSource = Preconditions.CheckNotNull(configSource, nameof(configSource));
        }

        static CreateContainerParameters CloneOrCreateParams(CreateContainerParameters createOptions) =>
            createOptions != null
            ? JsonConvert.DeserializeObject<CreateContainerParameters>(JsonConvert.SerializeObject(createOptions))
            : new CreateContainerParameters();

        public override CombinedDockerConfig GetCombinedConfig(IModule module, IRuntimeInfo runtimeInfo)
        {
            CombinedDockerConfig combinedConfig = base.GetCombinedConfig(module, runtimeInfo);

            // if the workload URI is a Unix domain socket then volume mount it into the container
            CreateContainerParameters createOptions = CloneOrCreateParams(combinedConfig.CreateOptions);
            this.MountSockets(module, createOptions);
            this.InjectNetworkAliases(module, createOptions);

            return new CombinedDockerConfig(combinedConfig.Image, createOptions, combinedConfig.AuthConfig);
        }

        void InjectNetworkAliases(IModule module, CreateContainerParameters createOptions)
        {
            if (createOptions.NetworkingConfig?.EndpointsConfig == null &&
                !string.IsNullOrWhiteSpace(this.configSource.AppSettings.NetworkId))
            {
                var endpointSettings = new EndpointSettings();

                if (module.Name.Equals(Constants.EdgeHubModuleName, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(this.configSource.AppSettings.EdgeDeviceHostName))
                {
                    endpointSettings.Aliases = new List<string>
                    {
                        this.configSource.AppSettings.EdgeDeviceHostName
                    };
                }

                createOptions.NetworkingConfig = new NetworkingConfig
                {
                    EndpointsConfig = new Dictionary<string, EndpointSettings>
                    {
                        [this.configSource.AppSettings.NetworkId] = endpointSettings
                    }
                };
            }
        }

        void MountSockets(IModule module, CreateContainerParameters createOptions)
        {
            var workloadUri = new Uri(this.configSource.AppSettings.WorkloadUri);
            if (workloadUri.Scheme == "unix")
            {
                SetMountOptions(createOptions, workloadUri);
            }

            // If Management URI is Unix domain socket, and the module is the EdgeAgent, then mount it ino the container.
            var managementUri = new Uri(this.configSource.AppSettings.ManagementUri);
            if (managementUri.Scheme == "unix"
                && module.Name.Equals(Constants.EdgeAgentModuleName, StringComparison.OrdinalIgnoreCase))
            {
                SetMountOptions(createOptions, managementUri);
            }
        }

        static void SetMountOptions(CreateContainerParameters createOptions, Uri uri)
        {
            HostConfig hostConfig = createOptions.HostConfig ?? new HostConfig();
            IList<string> binds = hostConfig.Binds ?? new List<string>();
            binds.Add($"{uri.AbsolutePath}:{uri.AbsolutePath}");

            hostConfig.Binds = binds;
            createOptions.HostConfig = hostConfig;
        }
    }
}
