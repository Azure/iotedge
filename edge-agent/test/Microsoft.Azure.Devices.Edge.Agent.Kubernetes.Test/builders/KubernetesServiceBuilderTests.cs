// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Test
{
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
            var config = new CombinedDockerConfig("image", new Docker.Models.CreateContainerParameters(), Option.None<AuthConfig>());
            var m1 = new DockerModule("module1", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
            config.CreateOptions.Labels = new Dictionary<string, string>
            {
                // string.Empty is an invalid label name
                { string.Empty, "test" }
            };
            var km1 = new KubernetesModule(m1, config);
            var builder = new KubernetesServiceBuilder(PortMapServiceType.ClusterIP);
            var moduleLabels = new Dictionary<string, string>();

            Assert.Throws<InvalidKubernetesNameException>(() => builder.GetServiceFromModule(moduleLabels, km1, null));
        }

        [Unit]
        [Fact]
        public void NoPortsExposedMeansNoServiceCreated()
        {
            var config = new CombinedDockerConfig("image", new Docker.Models.CreateContainerParameters(), Option.None<AuthConfig>());
            var m1 = new DockerModule("module1", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
            var km1 = new KubernetesModule(m1, config);
            var builder = new KubernetesServiceBuilder(PortMapServiceType.ClusterIP);
            var moduleLabels = new Dictionary<string, string>();

            var service = builder.GetServiceFromModule(moduleLabels, km1, null);

            Assert.False(service.HasValue);
        }

        [Unit]
        [Fact]
        public void InvalidPortBindingDoesNotCreateAService()
        {
            var config = new CombinedDockerConfig("image", new Docker.Models.CreateContainerParameters(), Option.None<AuthConfig>());
            var identity = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "moduleid", Mock.Of<ICredentials>());
            config.CreateOptions.ExposedPorts = new Dictionary<string, EmptyStruct>
            {
                // Add invalid port
                { "aa/TCP", default(EmptyStruct) }
            };
            var m1 = new DockerModule("module1", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
            var km1 = new KubernetesModule(m1, config);
            var builder = new KubernetesServiceBuilder(PortMapServiceType.ClusterIP);
            var moduleLabels = new Dictionary<string, string>();

            var service = builder.GetServiceFromModule(moduleLabels, km1, identity);

            Assert.False(service.HasValue);
        }

        [Unit]
        [Fact]
        public void UnknownProtocolDoesNotCreateService()
        {
            var config = new CombinedDockerConfig("image", new Docker.Models.CreateContainerParameters(), Option.None<AuthConfig>());
            var identity = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "moduleid", Mock.Of<ICredentials>());
            config.CreateOptions.ExposedPorts = new Dictionary<string, EmptyStruct>
            {
                // Add unknown protocol
                { "123/XXX", default(EmptyStruct) }
            };
            var m1 = new DockerModule("module1", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
            var km1 = new KubernetesModule(m1, config);
            var builder = new KubernetesServiceBuilder(PortMapServiceType.ClusterIP);
            var moduleLabels = new Dictionary<string, string>();

            var service = builder.GetServiceFromModule(moduleLabels, km1, identity);

            Assert.False(service.HasValue);
        }

        [Unit]
        [Fact]
        public void ExposingPortsCreatesAServiceHappyPath()
        {
            var config = new CombinedDockerConfig("image", new Docker.Models.CreateContainerParameters(), Option.None<AuthConfig>());
            var identity = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "moduleid", Mock.Of<ICredentials>());
            config.CreateOptions.ExposedPorts = new Dictionary<string, EmptyStruct>
            {
                // Add a port to be exposed
                { "10/TCP", default(EmptyStruct) }
            };
            var m1 = new DockerModule("module1", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
            var km1 = new KubernetesModule(m1, config);
            var builder = new KubernetesServiceBuilder(PortMapServiceType.ClusterIP);
            var moduleLabels = new Dictionary<string, string>();

            var service = builder.GetServiceFromModule(moduleLabels, km1, identity);

            Assert.True(service.HasValue);
        }

        [Unit]
        [Fact]
        public void ServiceCreationHappyPath()
        {
            var config = new CombinedDockerConfig("image", new Docker.Models.CreateContainerParameters(), Option.None<AuthConfig>());
            var identity = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "moduleid", Mock.Of<ICredentials>());
            var moduleLabels = new Dictionary<string, string> { { "label1", "value1" } };
            config.CreateOptions.ExposedPorts = new Dictionary<string, EmptyStruct>
            {
                // Add a port to be exposed
                { "10/TCP", default(EmptyStruct) }
            };
            var m1 = new DockerModule("module1", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
            var km1 = new KubernetesModule(m1, config);
            var builder = new KubernetesServiceBuilder(PortMapServiceType.ClusterIP);

            var converted = builder.GetServiceFromModule(moduleLabels, km1, identity);
            Assert.True(converted.HasValue);
            var service = converted.OrDefault();
            Assert.True(service.Spec.Ports.Count == 1);
            Assert.Equal("ClusterIP", service.Spec.Type);
            Assert.True(service.Spec.Selector.Keys.Count == 1);
        }
    }
}
