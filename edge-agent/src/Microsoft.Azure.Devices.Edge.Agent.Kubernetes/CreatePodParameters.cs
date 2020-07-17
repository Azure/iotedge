// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    using System.Collections.Generic;
    using System.Linq;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Docker.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment.Service;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Json;
    using Newtonsoft.Json;
    using EmptyStruct = global::Docker.DotNet.Models.EmptyStruct;

    public class CreatePodParameters
    {
        public CreatePodParameters(
            IEnumerable<string> env,
            IDictionary<string, EmptyStruct> exposedPorts,
            HostConfig hostConfig,
            string image,
            IDictionary<string, string> labels,
            IEnumerable<string> cmd,
            IEnumerable<string> entrypoint,
            string workingDir)
            : this(env?.ToList(), exposedPorts, hostConfig, image, labels, cmd?.ToList(), entrypoint?.ToList(), workingDir, null, null, null, null, null, null)
        {
        }

        [JsonConstructor]
        CreatePodParameters(
            IReadOnlyList<string> env,
            IDictionary<string, EmptyStruct> exposedPorts,
            HostConfig hostConfig,
            string image,
            IDictionary<string, string> labels,
            IReadOnlyList<string> cmd,
            IReadOnlyList<string> entrypoint,
            string workingDir,
            IDictionary<string, string> nodeSelector,
            V1ResourceRequirements resources,
            IReadOnlyList<KubernetesModuleVolumeSpec> volumes,
            V1PodSecurityContext securityContext,
            KubernetesServiceOptions serviceOptions,
            V1DeploymentStrategy strategy)
        {
            this.Env = Option.Maybe(env);
            this.ExposedPorts = Option.Maybe(exposedPorts);
            this.HostConfig = Option.Maybe(hostConfig);
            this.Image = Option.Maybe(image);
            this.Labels = Option.Maybe(labels);
            this.Cmd = Option.Maybe(cmd);
            this.Entrypoint = Option.Maybe(entrypoint);
            this.WorkingDir = Option.Maybe(workingDir);
            this.NodeSelector = Option.Maybe(nodeSelector);
            this.Resources = Option.Maybe(resources);
            this.Volumes = Option.Maybe(volumes);
            this.SecurityContext = Option.Maybe(securityContext);
            this.ServiceOptions = Option.Maybe(serviceOptions);
            this.DeploymentStrategy = Option.Maybe(strategy);
        }

        internal static CreatePodParameters Create(
            IReadOnlyList<string> env = null,
            IDictionary<string, EmptyStruct> exposedPorts = null,
            HostConfig hostConfig = null,
            string image = null,
            IDictionary<string, string> labels = null,
            IReadOnlyList<string> cmd = null,
            IReadOnlyList<string> entrypoint = null,
            string workingDir = null,
            IDictionary<string, string> nodeSelector = null,
            V1ResourceRequirements resources = null,
            IReadOnlyList<KubernetesModuleVolumeSpec> volumes = null,
            V1PodSecurityContext securityContext = null,
            KubernetesServiceOptions serviceOptions = null,
            V1DeploymentStrategy deploymentStrategy = null)
            => new CreatePodParameters(env, exposedPorts, hostConfig, image, labels, cmd, entrypoint, workingDir, nodeSelector, resources, volumes, securityContext, serviceOptions, deploymentStrategy);

        [JsonProperty("env", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [JsonConverter(typeof(OptionConverter<IReadOnlyList<string>>))]
        public Option<IReadOnlyList<string>> Env { get; }

        [JsonProperty("exposedPorts", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [JsonConverter(typeof(OptionConverter<IDictionary<string, EmptyStruct>>))]
        public Option<IDictionary<string, EmptyStruct>> ExposedPorts { get; }

        [JsonProperty("hostConfig", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [JsonConverter(typeof(OptionConverter<HostConfig>))]
        public Option<HostConfig> HostConfig { get; }

        [JsonProperty("image", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [JsonConverter(typeof(OptionConverter<string>))]
        public Option<string> Image { get; }

        [JsonProperty("labels", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [JsonConverter(typeof(OptionConverter<IDictionary<string, string>>))]
        public Option<IDictionary<string, string>> Labels { get; }

        [JsonProperty("nodeSelector", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [JsonConverter(typeof(OptionConverter<IDictionary<string, string>>))]
        public Option<IDictionary<string, string>> NodeSelector { get; set; }

        [JsonProperty("resources", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [JsonConverter(typeof(OptionConverter<V1ResourceRequirements>))]
        public Option<V1ResourceRequirements> Resources { get; set; }

        [JsonProperty("volumes", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [JsonConverter(typeof(OptionConverter<IReadOnlyList<KubernetesModuleVolumeSpec>>))]
        public Option<IReadOnlyList<KubernetesModuleVolumeSpec>> Volumes { get; set; }

        [JsonProperty("securityContext", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [JsonConverter(typeof(OptionConverter<V1PodSecurityContext>))]
        public Option<V1PodSecurityContext> SecurityContext { get; set; }

        [JsonProperty("serviceOptions", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [JsonConverter(typeof(OptionConverter<KubernetesServiceOptions>))]
        public Option<KubernetesServiceOptions> ServiceOptions { get; set; }

        [JsonProperty("strategy", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [JsonConverter(typeof(OptionConverter<V1DeploymentStrategy>))]
        public Option<V1DeploymentStrategy> DeploymentStrategy { get; set; }

        [JsonProperty("cmd", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [JsonConverter(typeof(OptionConverter<IReadOnlyList<string>>))]
        public Option<IReadOnlyList<string>> Cmd { get; }

        [JsonProperty("entrypoint", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [JsonConverter(typeof(OptionConverter<IReadOnlyList<string>>))]
        public Option<IReadOnlyList<string>> Entrypoint { get; }

        [JsonProperty("workingDir", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [JsonConverter(typeof(OptionConverter<string>))]
        public Option<string> WorkingDir { get; }
    }
}
