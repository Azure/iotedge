// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Test
{
    using System.Collections.Generic;
    using System.Linq;
    using global::Docker.DotNet.Models;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Unit]
    public class KubernetesPodBuilderTests
    {
        static readonly ConfigurationInfo DefaultConfigurationInfo = new ConfigurationInfo("1");
        static readonly IDictionary<string, EnvVal> EnvVarsDict = new Dictionary<string, EnvVal>();
        static readonly List<V1EnvVar> EnvVars = new List<V1EnvVar>();
        static readonly DockerConfig Config1 = new DockerConfig("test-image:1");

        [Unit]
        [Fact]
        public void EmptyIsNotAllowedAsPodAnnotation()
        {
            var identity = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "moduleid", Mock.Of<ICredentials>());
            var config = new CombinedDockerConfig("image", new global::Microsoft.Azure.Devices.Edge.Agent.Docker.Models.CreateContainerParameters(), Option.None<AuthConfig>());
            config.CreateOptions.Labels = new Dictionary<string, string>
            {
                // string.Empty is an invalid label name
                { string.Empty, "test" }
            };
            var m1 = new DockerModule("module1", "v1", ModuleStatus.Running, global::Microsoft.Azure.Devices.Edge.Agent.Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVarsDict);
            var km1 = new KubernetesModule(m1, config);
            var builder = new KubernetesPodBuilder("image", "configPath", "configVolumeName", "configMapName", "trustBundlePAth", "trustBundleVolumeName", "trustBindleConfigMapName");
            var labels = new Dictionary<string, string>();

            Assert.Throws<InvalidKubernetesNameException>(() => builder.GetPodFromModule(labels, km1, identity, EnvVars));
        }

        [Unit]
        [Fact]
        public void SimplePodCreationHappyPath()
        {
            var identity = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "moduleid", Mock.Of<ICredentials>());
            var config = new CombinedDockerConfig("image", new global::Microsoft.Azure.Devices.Edge.Agent.Docker.Models.CreateContainerParameters(), Option.None<AuthConfig>());
            var m1 = new DockerModule("module1", "v1", ModuleStatus.Running, global::Microsoft.Azure.Devices.Edge.Agent.Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVarsDict);
            var km1 = new KubernetesModule(m1, config);
            var builder = new KubernetesPodBuilder("image", "configPath", "configVolumeName", "configMapName", "trustBundlePAth", "trustBundleVolumeName", "trustBindleConfigMapName");
            var labels = new Dictionary<string, string>();

            var pod = builder.GetPodFromModule(labels, km1, identity, EnvVars);

            Assert.True(pod != null);
        }

        [Unit]
        [Fact]
        public void ValidatePodPropertyTranslation()
        {
            var identity = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "moduleid", Mock.Of<ICredentials>());
            var config = new CombinedDockerConfig("image", new global::Microsoft.Azure.Devices.Edge.Agent.Docker.Models.CreateContainerParameters(), Option.None<AuthConfig>());
            config.CreateOptions.Labels = new Dictionary<string, string>
            {
                // Add a label
                { "demo", "test" }
            };
            config.CreateOptions.HostConfig = new global::Microsoft.Azure.Devices.Edge.Agent.Docker.Models.HostConfig
            {
                // Make container privileged
                Privileged = true,
                // Add a readonly mount
                Binds = new List<string> { "/home/blah:/home/blah2:ro" }
            };
            var m1 = new DockerModule("module1", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVarsDict);
            var km1 = new KubernetesModule(m1, config);
            var builder = new KubernetesPodBuilder("image", "configPath", "configVolumeName", "configMapName", "trustBundlePAth", "trustBundleVolumeName", "trustBindleConfigMapName");
            var labels = new Dictionary<string, string>();

            var pod = builder.GetPodFromModule(labels, km1, identity, EnvVars);

            Assert.True(pod != null);
            // Validate annotation
            Assert.True(pod.Metadata.Annotations.ContainsKey("demo"));
            // Two containers should exist - proxy and the module
            Assert.True(pod.Spec.Containers.Count == 2);
            // There should only be one container
            var moduleContainer = pod.Spec.Containers.Single(p => p.Name != "proxy");
            // We made this container priviledged
            Assert.True(moduleContainer.SecurityContext.Privileged);
            // Validate that there are 4 mounts
            Assert.True(moduleContainer.VolumeMounts.Count == 4);
            // Validate the custom mount that we added
            Assert.Contains(moduleContainer.VolumeMounts, vm => vm.Name.Equals("homeblah"));
            var mount = moduleContainer.VolumeMounts.Single(vm => vm.Name.Equals("homeblah"));
            // Lets make sure that it is read only
            Assert.True(mount.ReadOnlyProperty);
        }
    }
}
