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
            ModuleIdentity identity = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "moduleid", Mock.Of<ICredentials>());
            CombinedDockerConfig config = new CombinedDockerConfig("image", new Docker.Models.CreateContainerParameters(), Option.None<AuthConfig>());

            // string.Empty is an invalid label name
            config.CreateOptions.Labels = new Dictionary<string, string>() { { string.Empty, "test" } };

            IModule m1 = new DockerModule("module1", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVarsDict);
            KubernetesModule km1 = new KubernetesModule(m1 as IModule<DockerConfig>, config);

            KubernetesPodBuilder builder = new KubernetesPodBuilder("image", "configPath", "configVolumeName", "trustBundlePAth", "trustBundleVolumeName");

            Dictionary<string, string> labels = new Dictionary<string, string>();

            Assert.Throws<InvalidKubernetesNameException>(() => builder.GetPodFromModule(labels, km1, identity, EnvVars));
        }

        [Unit]
        [Fact]
        public void SimplePodCreationHappyPath()
        {
            ModuleIdentity identity = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "moduleid", Mock.Of<ICredentials>());
            CombinedDockerConfig config = new CombinedDockerConfig("image", new Docker.Models.CreateContainerParameters(), Option.None<AuthConfig>());

            IModule m1 = new DockerModule("module1", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVarsDict);
            KubernetesModule km1 = new KubernetesModule(m1 as IModule<DockerConfig>, config);

            KubernetesPodBuilder builder = new KubernetesPodBuilder("image", "configPath", "configVolumeName", "trustBundlePAth", "trustBundleVolumeName");

            Dictionary<string, string> labels = new Dictionary<string, string>();

            var pod = builder.GetPodFromModule(labels, km1, identity, EnvVars);
            Assert.True(pod != null);
        }

        [Unit]
        [Fact]
        public void ValidatePodPropertyTranslation()
        {
            ModuleIdentity identity = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "moduleid", Mock.Of<ICredentials>());
            CombinedDockerConfig config = new CombinedDockerConfig("image", new Docker.Models.CreateContainerParameters(), Option.None<AuthConfig>());

            // Add a label
            config.CreateOptions.Labels = new Dictionary<string, string>() { { "demo", "test" } };

            // Make container privileged
            // Add a readonly mount
            config.CreateOptions.HostConfig = new Docker.Models.HostConfig()
            {
                Privileged = true,
                Binds = new List<string>() { "/home/blah:/home/blah2:ro" }
            };

            IModule m1 = new DockerModule("module1", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVarsDict);
            KubernetesModule km1 = new KubernetesModule(m1 as IModule<DockerConfig>, config);

            KubernetesPodBuilder builder = new KubernetesPodBuilder("image", "configPath", "configVolumeName", "trustBundlePAth", "trustBundleVolumeName");

            Dictionary<string, string> labels = new Dictionary<string, string>();

            var pod = builder.GetPodFromModule(labels, km1, identity, EnvVars);
            Assert.True(pod != null);

            // Validate annotation
            Assert.True(pod.Metadata.Annotations.ContainsKey("demo"));

            // Two containers should exist - proxy and the module
            Assert.True(pod.Spec.Containers.Count == 2);

            // There should only be one container
            var moduleContainer = pod.Spec.Containers.Where(p => p.Name != "proxy").Single();

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
