// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Test.builders
{
    using System.Collections;
    using System.Collections.Generic;
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
        public void PodCreationHappyPath()
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
    }
}
