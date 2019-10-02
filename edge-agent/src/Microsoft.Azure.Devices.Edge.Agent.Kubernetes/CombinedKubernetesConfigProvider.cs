// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.InteropServices;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Microsoft.Azure.Devices.Edge.Agent.Docker.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    public class CombinedKubernetesConfigProvider : ICombinedConfigProvider<CombinedDockerConfig>, ICombinedConfigProvider<CombinedKubernetesConfig>
    {
        readonly CombinedDockerConfigProvider dockerConfigProvider;
        readonly string edgeDeviceHostName;
        readonly string networkId;
        readonly Uri workloadUri;
        readonly Uri managementUri;
        readonly bool enableKubernetesExtensions;

        public CombinedKubernetesConfigProvider(
            IEnumerable<global::Docker.DotNet.Models.AuthConfig> authConfigs,
            string edgeDeviceHostName,
            string networkId,
            Uri workloadUri,
            Uri managementUri,
            bool enableKubernetesExtensions)
        {
            this.dockerConfigProvider = new CombinedDockerConfigProvider(authConfigs);
            this.edgeDeviceHostName = Preconditions.CheckNotNull(edgeDeviceHostName, nameof(edgeDeviceHostName));
            this.networkId = Preconditions.CheckNotNull(networkId, nameof(networkId));
            this.workloadUri = Preconditions.CheckNotNull(workloadUri, nameof(workloadUri));
            this.managementUri = Preconditions.CheckNotNull(managementUri, nameof(managementUri));
            this.enableKubernetesExtensions = enableKubernetesExtensions;
        }

        CombinedDockerConfig ICombinedConfigProvider<CombinedDockerConfig>.GetCombinedConfig(IModule module, IRuntimeInfo runtimeInfo)
            => this.GetCombinedConfig(module, runtimeInfo);

        CombinedKubernetesConfig ICombinedConfigProvider<CombinedKubernetesConfig>.GetCombinedConfig(IModule module, IRuntimeInfo runtimeInfo)
        {
            CombinedDockerConfig dockerConfig = this.GetCombinedConfig(module, runtimeInfo);

            CreatePodParameters createOptions = new CreatePodParameters
            {
                Env = dockerConfig.CreateOptions.Env,
                Image = dockerConfig.CreateOptions.Image,
                Labels = dockerConfig.CreateOptions.Labels,
                ExposedPorts = dockerConfig.CreateOptions.ExposedPorts,
                HostConfig = dockerConfig.CreateOptions.HostConfig,
                NetworkingConfig = dockerConfig.CreateOptions.NetworkingConfig,
            };

            if (this.enableKubernetesExtensions)
            {
                Option<KubernetesExperimentalCreatePodParameters> experimentalOptions = KubernetesExperimentalCreatePodParameters.Parse(dockerConfig.CreateOptions.OtherProperties);
                // experimentalOptions.ForEach(parameters => createOptions.Volumes = parameters.Volumes);
                experimentalOptions.ForEach(parameters => createOptions.NodeSelector = parameters.NodeSelector);
                // experimentalOptions.ForEach(parameters => createOptions.Resources = parameters.Resources);
            }

            Option<AuthConfig> authConfig = dockerConfig.AuthConfig
                .Map(auth => new AuthConfig(ImagePullSecretName.Create(auth)));

            return new CombinedKubernetesConfig(dockerConfig.Image, createOptions, authConfig);
        }

        CombinedDockerConfig GetCombinedConfig(IModule module, IRuntimeInfo runtimeInfo)
        {
            CombinedDockerConfig combinedConfig = this.dockerConfigProvider.GetCombinedConfig(module, runtimeInfo);

            // if the workload URI is a Unix domain socket then volume mount it into the container
            CreateContainerParameters createOptions = CloneOrCreateParams(combinedConfig.CreateOptions);
            this.MountSockets(module, createOptions);

            if (!string.IsNullOrWhiteSpace(this.networkId))
            {
                this.InjectNetworkAliases(module, createOptions);
            }

            return new CombinedDockerConfig(combinedConfig.Image, createOptions, combinedConfig.AuthConfig);
        }

        static CreateContainerParameters CloneOrCreateParams(CreateContainerParameters createOptions) =>
            createOptions != null
                ? JsonConvert.DeserializeObject<CreateContainerParameters>(JsonConvert.SerializeObject(createOptions))
                : new CreateContainerParameters();

        void MountSockets(IModule module, CreateContainerParameters createOptions)
        {
            if (string.Equals(this.workloadUri.Scheme, "unix", StringComparison.OrdinalIgnoreCase))
            {
                SetMountOptions(createOptions, this.workloadUri);
            }

            // If Management URI is Unix domain socket, and the module is the EdgeAgent, then mount it ino the container.
            if (string.Equals(this.managementUri.Scheme, "unix", StringComparison.OrdinalIgnoreCase)
                && module.Name.Equals(Core.Constants.EdgeAgentModuleName, StringComparison.OrdinalIgnoreCase))
            {
                SetMountOptions(createOptions, this.managementUri);
            }
        }

        static void SetMountOptions(CreateContainerParameters createOptions, Uri uri)
        {
            HostConfig hostConfig = createOptions.HostConfig ?? new HostConfig();
            IList<string> binds = hostConfig.Binds ?? new List<string>();
            string path = BindPath(uri);
            binds.Add($"{path}:{path}");

            hostConfig.Binds = binds;
            createOptions.HostConfig = hostConfig;
        }

        static string BindPath(Uri uri)
        {
            // On Windows we need to bind to the parent folder. We can't bind
            // directly to the socket file.
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? Path.GetDirectoryName(uri.LocalPath)
                : uri.AbsolutePath;
        }

        void InjectNetworkAliases(IModule module, CreateContainerParameters createOptions)
        {
            if (createOptions.NetworkingConfig?.EndpointsConfig != null)
            {
                return;
            }

            var endpointSettings = new EndpointSettings();
            if (module.Name.Equals(Core.Constants.EdgeHubModuleName, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(this.edgeDeviceHostName))
            {
                endpointSettings.Aliases = new List<string>
                {
                    this.edgeDeviceHostName
                };
            }

            createOptions.NetworkingConfig = new NetworkingConfig
            {
                EndpointsConfig = new Dictionary<string, EndpointSettings>
                {
                    [this.networkId] = endpointSettings
                }
            };
        }
    }
}
