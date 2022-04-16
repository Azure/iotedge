// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Edgelet.Docker
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.InteropServices;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Microsoft.Azure.Devices.Edge.Agent.Docker.Models;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;
    using Newtonsoft.Json;
    using AuthConfig = global::Docker.DotNet.Models.AuthConfig;

    public class CombinedEdgeletConfigProvider : CombinedDockerConfigProvider
    {
        readonly IConfigSource configSource;

        public CombinedEdgeletConfigProvider(
            IEnumerable<AuthConfig> authConfigs,
            IConfigSource configSource)
            : base(authConfigs)
        {
            this.configSource = Preconditions.CheckNotNull(configSource, nameof(configSource));
        }

        public override CombinedDockerConfig GetCombinedConfig(IModule module, IRuntimeInfo runtimeInfo)
        {
            CombinedDockerConfig combinedConfig = base.GetCombinedConfig(module, runtimeInfo);

            CreateContainerParameters createOptions = CloneOrCreateParams(combinedConfig.CreateOptions);

            // before making any other modifications to createOptions, save edge agent's createOptions + env as
            // container labels so they're available as soon as it loads
            InjectEdgeAgentLabels(module, createOptions);

            // if the workload URI is a Unix domain socket then volume mount it into the container
            this.MountSockets(module, createOptions);
            this.InjectNetworkAliases(module, createOptions);

            return new CombinedDockerConfig(combinedConfig.Image, createOptions, combinedConfig.AuthConfig);
        }

        static CreateContainerParameters CloneOrCreateParams(CreateContainerParameters createOptions) =>
            createOptions != null
                ? JsonConvert.DeserializeObject<CreateContainerParameters>(JsonConvert.SerializeObject(createOptions))
                : new CreateContainerParameters();

        static void SetMountOptions(CreateContainerParameters createOptions, Uri listenUri, Uri connectUri)
        {
            HostConfig hostConfig = createOptions.HostConfig ?? new HostConfig();
            IList<string> binds = hostConfig.Binds ?? new List<string>();
            string sourcePath = BindPath(listenUri);
            string targetPath = BindPath(connectUri);

            binds.Add($"{sourcePath}:{targetPath}");

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

        static void InjectEdgeAgentLabels(IModule module, CreateContainerParameters createOptions)
        {
            // The IModule argument came from the desired properties in a new deployment. For edge agent only,
            // save its createOptions and env as labels so we can detect changes in future configs.
            // Note: createOptions and env for each module are generally saved to a store (see the DeploymentConfigInfo
            // member of Microsoft.Azure.Devices.Edge.Agent.Core.Agent) and we could get them there, but we'd miss the
            // bootstrap scenario where edge agent is starting for the first time and therefore has no config store.
            // That's why edge agent is treated differently: its config is persisted in the container's labels before
            // it ever starts.
            if (module.Name.Equals(Constants.EdgeAgentModuleName, StringComparison.OrdinalIgnoreCase))
            {
                // get createOptions JSON before making any updates to labels
                string createOptionsJson = JsonConvert.SerializeObject(createOptions);

                var moduleWithDockerConfig = (IModule<DockerConfig>)module; // cast is safe; base impl already checked it
                var env = moduleWithDockerConfig.Env ?? new Dictionary<string, EnvVal>();

                var labels = createOptions.Labels ?? new Dictionary<string, string>();
                // if these labels already existed (e.g. specified in the deployment), just overwrite them
                labels[Constants.Labels.CreateOptions] = createOptionsJson;
                labels[Constants.Labels.Env] = JsonConvert.SerializeObject(env);
                createOptions.Labels = labels;
            }
        }

        void InjectNetworkAliases(IModule module, CreateContainerParameters createOptions)
        {
            if (createOptions.NetworkingConfig?.EndpointsConfig == null)
            {
                string networkId = this.configSource.Configuration.GetValue<string>(Constants.NetworkIdKey);
                string edgeDeviceHostName = this.configSource.Configuration.GetValue<string>(Constants.EdgeDeviceHostNameKey);

                if (!string.IsNullOrWhiteSpace(networkId))
                {
                    var endpointSettings = new EndpointSettings();
                    if (module.Name.Equals(Constants.EdgeHubModuleName, StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrWhiteSpace(edgeDeviceHostName))
                    {
                        endpointSettings.Aliases = new List<string>
                        {
                            edgeDeviceHostName
                        };
                    }

                    IDictionary<string, EndpointSettings> endpointsConfig = new Dictionary<string, EndpointSettings>
                    {
                        [networkId] = endpointSettings
                    };
                    createOptions.NetworkingConfig = new NetworkingConfig
                    {
                        EndpointsConfig = endpointsConfig
                    };
                }
            }
        }

        void MountSockets(IModule module, CreateContainerParameters createOptions)
        {
            var workloadListenUriMntString = this.configSource.Configuration.GetValue<string>(Constants.EdgeletWorkloadListenMntUriVariableName);
            var workloadConnectUri = new Uri(this.configSource.Configuration.GetValue<string>(Constants.EdgeletWorkloadUriVariableName));

            Uri workloadListenUri = !string.IsNullOrEmpty(workloadListenUriMntString)
              ? new Uri($"{workloadListenUriMntString}/{module.Name}.sock")
              : workloadConnectUri;

            if (workloadConnectUri.Scheme == "unix")
            {
                SetMountOptions(createOptions, workloadListenUri, workloadConnectUri);
            }

            // If Management URI is Unix domain socket, and the module is the EdgeAgent, then mount it into the container.
            var managementUri = new Uri(this.configSource.Configuration.GetValue<string>(Constants.EdgeletManagementUriVariableName));
            if (managementUri.Scheme == "unix"
                && module.Name.Equals(Constants.EdgeAgentModuleName, StringComparison.OrdinalIgnoreCase))
            {
                SetMountOptions(createOptions, managementUri, managementUri);
            }
        }
    }
}
