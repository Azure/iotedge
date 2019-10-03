// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using global::Docker.DotNet.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment.Deployment;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Unit]
    public class KubernetesDeploymentMapperTest
    {
        static readonly ConfigurationInfo DefaultConfigurationInfo = new ConfigurationInfo("1");
        static readonly IDictionary<string, EnvVal> EnvVarsDict = new Dictionary<string, EnvVal>();
        static readonly DockerConfig Config1 = new DockerConfig("test-image:1");

        [Fact]
        public void EmptyIsNotAllowedAsPodAnnotation()
        {
            var identity = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "ModuleId", Mock.Of<ICredentials>());
            var config = new CombinedDockerConfig("image", new global::Microsoft.Azure.Devices.Edge.Agent.Docker.Models.CreateContainerParameters(), Option.None<AuthConfig>());
            config.CreateOptions.Labels = new Dictionary<string, string>
            {
                // string.Empty is an invalid label name
                { string.Empty, "test" }
            };
            var docker = new DockerModule("module1", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVarsDict);
            var module = new KubernetesModule(docker, config);
            var mapper = new KubernetesDeploymentMapper("namespace", "edgehub", "proxy", "configPath", "configVolumeName", "configMapName", "trustBundlePAth", "trustBundleVolumeName", "trustBindleConfigMapName", "apiVersion", new Uri("http://workload"), new Uri("http://management"));
            var labels = new Dictionary<string, string>();

            Assert.Throws<InvalidKubernetesNameException>(() => mapper.CreateDeployment(identity, module, labels));
        }

        [Fact]
        public void SimplePodCreationHappyPath()
        {
            var identity = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "ModuleId", Mock.Of<ICredentials>());
            var config = new CombinedDockerConfig("image", new global::Microsoft.Azure.Devices.Edge.Agent.Docker.Models.CreateContainerParameters(), Option.None<AuthConfig>());
            var docker = new DockerModule("module1", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVarsDict);
            var module = new KubernetesModule(docker, config);
            var mapper = new KubernetesDeploymentMapper("namespace", "edgehub", "proxy", "configPath", "configVolumeName", "configMapName", "trustBundlePAth", "trustBundleVolumeName", "trustBindleConfigMapName", "apiVersion", new Uri("http://workload"), new Uri("http://management"));
            var labels = new Dictionary<string, string>();

            var pod = mapper.CreateDeployment(identity, module, labels);

            Assert.True(pod != null);
        }

        [Fact]
        public void ValidatePodPropertyTranslation()
        {
            var identity = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "ModuleId", Mock.Of<ICredentials>());
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
            var docker = new DockerModule("module1", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVarsDict);
            var module = new KubernetesModule(docker, config);
            var mapper = new KubernetesDeploymentMapper("namespace", "edgehub", "proxy", "configPath", "configVolumeName", "configMapName", "trustBundlePAth", "trustBundleVolumeName", "trustBindleConfigMapName", "apiVersion", new Uri("http://workload"), new Uri("http://management"));
            var labels = new Dictionary<string, string>();

            var deployment = mapper.CreateDeployment(identity, module, labels);
            var pod = deployment.Spec.Template;

            Assert.NotNull(pod);
            // Validate annotation
            Assert.True(pod.Metadata.Annotations.ContainsKey("demo"));
            // Two containers should exist - proxy and the module
            Assert.Equal(2, pod.Spec.Containers.Count);
            // There should only be one container
            var moduleContainer = pod.Spec.Containers.Single(p => p.Name != "proxy");
            // We made this container privileged
            Assert.True(moduleContainer.SecurityContext.Privileged);
            // Validate that there are 4 mounts
            Assert.Equal(3, moduleContainer.VolumeMounts.Count);
            // Validate the custom mount that we added
            Assert.Contains(moduleContainer.VolumeMounts, vm => vm.Name.Equals("homeblah"));
            var mount = moduleContainer.VolumeMounts.Single(vm => vm.Name.Equals("homeblah"));
            // Lets make sure that it is read only
            Assert.True(mount.ReadOnlyProperty);
        }
    }
}
