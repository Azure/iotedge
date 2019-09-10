// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Test
{
    using System;
    using System.Collections.Generic;
    using global::Docker.DotNet.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Unit]
    public class KubernetesServiceBuilderTests
    {
        static readonly ConfigurationInfo DefaultConfigurationInfo = new ConfigurationInfo("1");
        static readonly IDictionary<string, EnvVal> EnvVars = new Dictionary<string, EnvVal>();
        static readonly DockerConfig Config1 = new DockerConfig("test-image:1");

        [Unit]
        [Fact]
        public void EmptyIsNotAllowedAsServiceAnnotation()
        {
            CombinedDockerConfig config = new CombinedDockerConfig("image", new Docker.Models.CreateContainerParameters(), Option.None<AuthConfig>());
            IModule m1 = new DockerModule("module1", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);

            // string.Empty is an invalid label name
            config.CreateOptions.Labels = new Dictionary<string, string>() { { string.Empty, "test" } };

            KubernetesModule km1 = new KubernetesModule(m1 as IModule<DockerConfig>, config);

            KubernetesServiceBuilder builder = new KubernetesServiceBuilder("TestMapServiceType");

            Dictionary<string, string> moduleLabels = new Dictionary<string, string>();

            Assert.Throws<InvalidKubernetesNameException>(() => builder.GetServiceFromModule(moduleLabels, km1, null));
        }

        [Unit]
        [Fact]
        public void NoPortsExposedMeansNoServiceCreated()
        {
            CombinedDockerConfig config = new CombinedDockerConfig("image", new Docker.Models.CreateContainerParameters(), Option.None<AuthConfig>());
            IModule m1 = new DockerModule("module1", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
            KubernetesModule km1 = new KubernetesModule(m1 as IModule<DockerConfig>, config);

            KubernetesServiceBuilder builder = new KubernetesServiceBuilder("TestMapServiceType");

            Dictionary<string, string> moduleLabels = new Dictionary<string, string>();

            Assert.False(builder.GetServiceFromModule(moduleLabels, km1, null).HasValue);
        }

        [Unit]
        [Fact]
        public void InvalidPortBindingDoesNotCreateAService()
        {
            CombinedDockerConfig config = new CombinedDockerConfig("image", new Docker.Models.CreateContainerParameters(), Option.None<AuthConfig>());
            ModuleIdentity identity = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "moduleid", Mock.Of<ICredentials>());

            config.CreateOptions.ExposedPorts = new Dictionary<string, EmptyStruct>() { { "aa/TCP", default(EmptyStruct) } };

            IModule m1 = new DockerModule("module1", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
            KubernetesModule km1 = new KubernetesModule(m1 as IModule<DockerConfig>, config);

            KubernetesServiceBuilder builder = new KubernetesServiceBuilder("TestMapServiceType");

            Dictionary<string, string> moduleLabels = new Dictionary<string, string>();

            Assert.False(builder.GetServiceFromModule(moduleLabels, km1, identity).HasValue);
        }

        [Unit]
        [Fact]
        public void UnknownProtocolDoesNotCreateService()
        {
            CombinedDockerConfig config = new CombinedDockerConfig("image", new Docker.Models.CreateContainerParameters(), Option.None<AuthConfig>());
            ModuleIdentity identity = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "moduleid", Mock.Of<ICredentials>());

            config.CreateOptions.ExposedPorts = new Dictionary<string, EmptyStruct>() { { "123/XXX", default(EmptyStruct) } };

            IModule m1 = new DockerModule("module1", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
            KubernetesModule km1 = new KubernetesModule(m1 as IModule<DockerConfig>, config);

            KubernetesServiceBuilder builder = new KubernetesServiceBuilder("TestMapServiceType");

            Dictionary<string, string> moduleLabels = new Dictionary<string, string>();

            Assert.False(builder.GetServiceFromModule(moduleLabels, km1, identity).HasValue);
        }

        [Unit]
        [Fact]
        public void ExposingPortsCreatesAServiceHappyPath()
        {
            CombinedDockerConfig config = new CombinedDockerConfig("image", new Docker.Models.CreateContainerParameters(), Option.None<AuthConfig>());
            ModuleIdentity identity = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "moduleid", Mock.Of<ICredentials>());

            // Add a port to be exposed
            config.CreateOptions.ExposedPorts = new Dictionary<string, EmptyStruct>() { { "10/TCP", default(EmptyStruct) } };

            IModule m1 = new DockerModule("module1", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
            KubernetesModule km1 = new KubernetesModule(m1 as IModule<DockerConfig>, config);

            KubernetesServiceBuilder builder = new KubernetesServiceBuilder("TestMapServiceType");

            Dictionary<string, string> moduleLabels = new Dictionary<string, string>();

            Assert.True(builder.GetServiceFromModule(moduleLabels, km1, identity).HasValue);
        }

        [Unit]
        [Fact]
        public void ServiceCreationHappyPath()
        {
            CombinedDockerConfig config = new CombinedDockerConfig("image", new Docker.Models.CreateContainerParameters(), Option.None<AuthConfig>());
            ModuleIdentity identity = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "moduleid", Mock.Of<ICredentials>());

            Dictionary<string, string> moduleLabels = new Dictionary<string, string>() { { "label1", "value1" } };

            // Add a port to be exposed
            config.CreateOptions.ExposedPorts = new Dictionary<string, EmptyStruct>() { { "10/TCP", default(EmptyStruct) } };

            IModule m1 = new DockerModule("module1", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
            KubernetesModule km1 = new KubernetesModule(m1 as IModule<DockerConfig>, config);

            KubernetesServiceBuilder builder = new KubernetesServiceBuilder("TestMapServiceType");

            var createdService = builder.GetServiceFromModule(moduleLabels, km1, identity);
            Assert.True(createdService.HasValue);
            var service = createdService.OrDefault();
            Assert.True(service.Spec.Ports.Count == 1);
            Assert.Equal("ClusterIP", service.Spec.Type);
            Assert.True(service.Spec.Selector.Keys.Count == 1);
        }
    }
}
