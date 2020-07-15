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
    using Newtonsoft.Json.Linq;

    public class CombinedKubernetesConfigProvider : ICombinedConfigProvider<CombinedKubernetesConfig>
    {
        const string CmdKey = "Cmd";
        const string EntrypointKey = "Entrypoint";
        const string WorkingDirKey = "WorkingDir";

        readonly CombinedDockerConfigProvider dockerConfigProvider;
        readonly Uri workloadUri;
        readonly Uri managementUri;
        readonly bool enableKubernetesExtensions;

        public CombinedKubernetesConfigProvider(
            IEnumerable<global::Docker.DotNet.Models.AuthConfig> authConfigs,
            Uri workloadUri,
            Uri managementUri,
            bool enableKubernetesExtensions)
        {
            this.dockerConfigProvider = new CombinedDockerConfigProvider(authConfigs);
            this.workloadUri = Preconditions.CheckNotNull(workloadUri, nameof(workloadUri));
            this.managementUri = Preconditions.CheckNotNull(managementUri, nameof(managementUri));
            this.enableKubernetesExtensions = enableKubernetesExtensions;
        }

        public CombinedKubernetesConfig GetCombinedConfig(IModule module, IRuntimeInfo runtimeInfo)
        {
            CombinedDockerConfig dockerConfig = this.dockerConfigProvider.GetCombinedConfig(module, runtimeInfo);

            // if the workload URI is a Unix domain socket then volume mount it into the container
            HostConfig hostConfig = this.AddSocketBinds(module, Option.Maybe(dockerConfig.CreateOptions.HostConfig));

            CreatePodParameters createOptions = new CreatePodParameters(
                dockerConfig.CreateOptions.Env,
                dockerConfig.CreateOptions.ExposedPorts,
                hostConfig,
                dockerConfig.CreateOptions.Image,
                dockerConfig.CreateOptions.Labels,
                GetPropertiesStringArray(CmdKey, dockerConfig.CreateOptions.OtherProperties),
                GetPropertiesStringArray(EntrypointKey, dockerConfig.CreateOptions.OtherProperties),
                GetPropertiesString(WorkingDirKey, dockerConfig.CreateOptions.OtherProperties));

            if (this.enableKubernetesExtensions)
            {
                Option<KubernetesExperimentalCreatePodParameters> experimentalOptions = KubernetesExperimentalCreatePodParameters.Parse(dockerConfig.CreateOptions.OtherProperties);
                experimentalOptions.ForEach(parameters => createOptions.Volumes = parameters.Volumes);
                experimentalOptions.ForEach(parameters => createOptions.NodeSelector = parameters.NodeSelector);
                experimentalOptions.ForEach(parameters => createOptions.Resources = parameters.Resources);
                experimentalOptions.ForEach(parameters => createOptions.SecurityContext = parameters.SecurityContext);
                experimentalOptions.ForEach(parameters => createOptions.ServiceOptions = parameters.ServiceOptions);
                experimentalOptions.ForEach(parameters => createOptions.DeploymentStrategy = parameters.DeploymentStrategy);
            }

            Option<ImagePullSecret> imagePullSecret = dockerConfig.AuthConfig
                .Map(auth => new ImagePullSecret(auth));

            return new CombinedKubernetesConfig(dockerConfig.Image, createOptions, imagePullSecret);
        }

        HostConfig AddSocketBinds(IModule module, Option<HostConfig> dockerHostConfig)
        {
            Option<HostConfig> hostConfig = dockerHostConfig;

            // If Workload URI is Unix domain socket, and the module is the EdgeAgent, then mount it ino the container.
            if (string.Equals(this.workloadUri.Scheme, "unix", StringComparison.OrdinalIgnoreCase))
            {
                string path = BindPath(this.workloadUri);
                hostConfig = hostConfig.Else(() => Option.Some(new HostConfig { Binds = new List<string>() }));
                hostConfig.ForEach(config => config.Binds.Add($"{path}:{path}"));
            }

            // If Management URI is Unix domain socket, and the module is the EdgeAgent, then mount it ino the container.
            if (string.Equals(this.managementUri.Scheme, "unix", StringComparison.OrdinalIgnoreCase)
                && module.Name.Equals(Core.Constants.EdgeAgentModuleName, StringComparison.OrdinalIgnoreCase))
            {
                string path = BindPath(this.managementUri);
                hostConfig = hostConfig.Else(() => Option.Some(new HostConfig { Binds = new List<string>() }));
                hostConfig.ForEach(config => config.Binds.Add($"{path}:{path}"));
            }

            return hostConfig.OrDefault();
        }

        static string BindPath(Uri uri)
        {
            // On Windows we need to bind to the parent folder. We can't bind
            // directly to the socket file.
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? Path.GetDirectoryName(uri.LocalPath)
                : uri.AbsolutePath;
        }

        static IReadOnlyList<string> GetPropertiesStringArray(string key, IDictionary<string, JToken> other) =>
            Option.Maybe(other).FlatMap(options => options.Get(key).FlatMap(option => Option.Maybe(option.ToObject<IReadOnlyList<string>>()))).OrDefault();

        static string GetPropertiesString(string key, IDictionary<string, JToken> other) =>
            Option.Maybe(other).FlatMap(options => options.Get(key).FlatMap(option => Option.Maybe(option.ToObject<string>()))).OrDefault();
    }
}
