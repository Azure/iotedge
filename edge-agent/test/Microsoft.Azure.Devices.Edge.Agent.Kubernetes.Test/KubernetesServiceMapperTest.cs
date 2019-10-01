// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Test
{
    using System.Collections.Generic;
    using global::Docker.DotNet.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment.Service;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Unit]
    public class KubernetesServiceMapperTest
    {
        static readonly ConfigurationInfo DefaultConfigurationInfo = new ConfigurationInfo("1");
        static readonly IDictionary<string, EnvVal> EnvVars = new Dictionary<string, EnvVal>();
        static readonly DockerConfig Config1 = new DockerConfig("test-image:1");

        [Fact]
        public void EmptyIsNotAllowedAsServiceAnnotation()
        {
            var config = new CombinedDockerConfig("image", new Docker.Models.CreateContainerParameters(), Option.None<AuthConfig>());
            config.CreateOptions.Labels = new Dictionary<string, string>
            {
                // string.Empty is an invalid label name
                { string.Empty, "test" }
            };
            var moduleId = new ModuleIdentity("hub", "gateway", "deviceId", "moduleid", Mock.Of<ICredentials>());
            var docker = new DockerModule(moduleId.ModuleId, "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
            var module = new KubernetesModule(docker, config);
            var moduleLabels = new Dictionary<string, string>();
            var mapper = new KubernetesServiceMapper(PortMapServiceType.ClusterIP);

            Assert.Throws<InvalidKubernetesNameException>(() => mapper.CreateService(moduleId, module, moduleLabels));
        }

        [Fact]
        public void NoPortsExposedMeansNoServiceCreated()
        {
            var config = new CombinedDockerConfig("image", new Docker.Models.CreateContainerParameters(), Option.None<AuthConfig>());
            var moduleId = new ModuleIdentity("hub", "gateway", "deviceId", "moduleid", Mock.Of<ICredentials>());
            var docker = new DockerModule("module1", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
            var module = new KubernetesModule(docker, config);
            var moduleLabels = new Dictionary<string, string>();

            var mapper = new KubernetesServiceMapper(PortMapServiceType.ClusterIP);

            var service = mapper.CreateService(moduleId, module, moduleLabels);
            Assert.False(service.HasValue);
        }

        [Fact]
        public void InvalidPortBindingDoesNotCreateAService()
        {
            var config = new CombinedDockerConfig("image", new Docker.Models.CreateContainerParameters(), Option.None<AuthConfig>());
            var moduleId = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "moduleid", Mock.Of<ICredentials>());
            config.CreateOptions.ExposedPorts = new Dictionary<string, EmptyStruct>
            {
                // Add invalid port
                { "aa/TCP", default(EmptyStruct) }
            };
            var docker = new DockerModule("module1", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
            var module = new KubernetesModule(docker, config);
            var moduleLabels = new Dictionary<string, string>();
            var mapper = new KubernetesServiceMapper(PortMapServiceType.ClusterIP);

            var service = mapper.CreateService(moduleId, module, moduleLabels);

            Assert.False(service.HasValue);
        }

        [Fact]
        public void UnknownProtocolDoesNotCreateService()
        {
            var config = new CombinedDockerConfig("image", new Docker.Models.CreateContainerParameters(), Option.None<AuthConfig>());
            var moduleId = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "moduleid", Mock.Of<ICredentials>());
            config.CreateOptions.ExposedPorts = new Dictionary<string, EmptyStruct>
            {
                // Add unknown protocol
                { "123/XXX", default(EmptyStruct) }
            };
            var docker = new DockerModule("module1", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
            var module = new KubernetesModule(docker, config);
            var moduleLabels = new Dictionary<string, string>();
            var mapper = new KubernetesServiceMapper(PortMapServiceType.ClusterIP);

            var service = mapper.CreateService(moduleId, module, moduleLabels);

            Assert.False(service.HasValue);
        }

        [Fact]
        public void ExposingPortsCreatesAServiceHappyPath()
        {
            var config = new CombinedDockerConfig("image", new Docker.Models.CreateContainerParameters(), Option.None<AuthConfig>());
            var moduleId = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "moduleid", Mock.Of<ICredentials>());
            config.CreateOptions.ExposedPorts = new Dictionary<string, EmptyStruct>
            {
                // Add a port to be exposed
                { "10/TCP", default(EmptyStruct) }
            };
            var docker = new DockerModule("module1", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
            var module = new KubernetesModule(docker, config);
            var moduleLabels = new Dictionary<string, string>();
            var mapper = new KubernetesServiceMapper(PortMapServiceType.ClusterIP);

            var service = mapper.CreateService(moduleId, module, moduleLabels);

            Assert.True(service.HasValue);
        }

        [Fact]
        public void ServiceCreationHappyPath()
        {
            var config = new CombinedDockerConfig("image", new Docker.Models.CreateContainerParameters(), Option.None<AuthConfig>());
            var moduleId = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "moduleid", Mock.Of<ICredentials>());
            var moduleLabels = new Dictionary<string, string> { { "label1", "value1" } };
            config.CreateOptions.ExposedPorts = new Dictionary<string, EmptyStruct>
            {
                // Add a port to be exposed
                { "10/TCP", default(EmptyStruct) }
            };
            var docker = new DockerModule("module1", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
            var module = new KubernetesModule(docker, config);
            var mapper = new KubernetesServiceMapper(PortMapServiceType.ClusterIP);

            var converted = mapper.CreateService(moduleId, module, moduleLabels);

            Assert.True(converted.HasValue);
            var service = converted.OrDefault();
            Assert.True(service.Spec.Ports.Count == 1);
            Assert.Equal("ClusterIP", service.Spec.Type);
            Assert.True(service.Spec.Selector.Keys.Count == 1);
        }
    }
}
