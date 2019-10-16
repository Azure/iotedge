// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Test
{
    using System.Collections.Generic;
    using System.Linq;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment.Service;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;
    using DockerModels = global::Microsoft.Azure.Devices.Edge.Agent.Docker.Models;
    using EmptyStruct = global::Docker.DotNet.Models.EmptyStruct;
    using KubernetesConstants = Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Constants;

    [Unit]
    public class KubernetesServiceMapperTest
    {
        static readonly ConfigurationInfo DefaultConfigurationInfo = new ConfigurationInfo("1");
        static readonly IDictionary<string, EnvVal> EnvVars = new Dictionary<string, EnvVal>();
        static readonly DockerConfig Config1 = new DockerConfig("test-image:1");

        [Fact]
        public void EmptyIsNotAllowedAsServiceAnnotation()
        {
            // string.Empty is an invalid label name
            var labels = new Dictionary<string, string> { { string.Empty, "test" } };
            var createOptions = CreatePodParameters.Create(labels: labels);
            var config = new KubernetesConfig("image", createOptions, Option.None<AuthConfig>());
            var moduleId = new ModuleIdentity("hub", "gateway", "deviceId", "moduleid", Mock.Of<ICredentials>());
            var docker = new DockerModule(moduleId.ModuleId, "v1", ModuleStatus.Running, RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
            var module = new KubernetesModule(docker, config);
            var moduleLabels = new Dictionary<string, string>();
            var mapper = new KubernetesServiceMapper(PortMapServiceType.ClusterIP);

            Assert.Throws<InvalidKubernetesNameException>(() => mapper.CreateService(moduleId, module, moduleLabels));
        }

        [Fact]
        public void NoPortsExposedMeansNoServiceCreated()
        {
            var createOptions = CreatePodParameters.Create();
            var config = new KubernetesConfig("image", createOptions, Option.None<AuthConfig>());
            var moduleId = new ModuleIdentity("hub", "gateway", "deviceId", "moduleid", Mock.Of<ICredentials>());
            var docker = new DockerModule("module1", "v1", ModuleStatus.Running, RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
            var module = new KubernetesModule(docker, config);
            var moduleLabels = new Dictionary<string, string>();

            var mapper = new KubernetesServiceMapper(PortMapServiceType.ClusterIP);

            var service = mapper.CreateService(moduleId, module, moduleLabels);
            Assert.False(service.HasValue);
        }

        [Fact]
        public void InvalidPortBindingDoesNotCreateAService()
        {
            // Add invalid port
            var exposedPorts = new Dictionary<string, EmptyStruct> { { "aa/TCP", default(EmptyStruct) } };
            var createOptions = CreatePodParameters.Create(exposedPorts: exposedPorts);
            var config = new KubernetesConfig("image", createOptions, Option.None<AuthConfig>());
            var moduleId = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "moduleid", Mock.Of<ICredentials>());
            var docker = new DockerModule("module1", "v1", ModuleStatus.Running, RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
            var module = new KubernetesModule(docker, config);
            var moduleLabels = new Dictionary<string, string>();
            var mapper = new KubernetesServiceMapper(PortMapServiceType.ClusterIP);

            var service = mapper.CreateService(moduleId, module, moduleLabels);

            Assert.False(service.HasValue);
        }

        [Fact]
        public void UnknownProtocolDoesNotCreateService()
        {
            // Add unknown protocol
            var exposedPorts = new Dictionary<string, EmptyStruct> { { "123/XXX", default(EmptyStruct) } };
            var createOptions = CreatePodParameters.Create(exposedPorts: exposedPorts);
            var config = new KubernetesConfig("image", createOptions, Option.None<AuthConfig>());
            var moduleId = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "moduleid", Mock.Of<ICredentials>());
            var docker = new DockerModule("module1", "v1", ModuleStatus.Running, RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
            var module = new KubernetesModule(docker, config);
            var moduleLabels = new Dictionary<string, string>();
            var mapper = new KubernetesServiceMapper(PortMapServiceType.ClusterIP);

            var service = mapper.CreateService(moduleId, module, moduleLabels);

            Assert.False(service.HasValue);
        }

        [Fact]
        public void DockerLabelsConvertedAsAnnotations()
        {
            var exposedPorts = new Dictionary<string, EmptyStruct> { ["10/TCP"] = default(EmptyStruct) };
            var labels = new Dictionary<string, string> { ["GPU"] = "Enabled" };
            var createOptions = CreatePodParameters.Create(exposedPorts: exposedPorts, labels: labels);
            var config = new KubernetesConfig("image", createOptions, Option.None<AuthConfig>());
            var docker = new DockerModule("module1", "v1", ModuleStatus.Running, RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
            var module = new KubernetesModule(docker, config);
            var moduleLabels = new Dictionary<string, string>();
            var mapper = new KubernetesServiceMapper(PortMapServiceType.ClusterIP);
            var moduleId = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "moduleid", Mock.Of<ICredentials>());

            var service = mapper.CreateService(moduleId, module, moduleLabels).OrDefault();

            Assert.Equal(1, service.Spec.Ports.Count);
            Assert.Equal(2, service.Metadata.Annotations.Count);
            Assert.NotNull(service.Metadata.Annotations[KubernetesConstants.CreationString]);
            Assert.Equal("Enabled", service.Metadata.Annotations["GPU"]);
            Assert.Equal(0, service.Metadata.Labels.Count);
            Assert.Equal(0, service.Spec.Selector.Count);
        }

        [Fact]
        public void LabelsConvertedAsLabelsAndSelectors()
        {
            var exposedPorts = new Dictionary<string, EmptyStruct> { ["10/TCP"] = default(EmptyStruct) };
            var createOptions = CreatePodParameters.Create(exposedPorts: exposedPorts);
            var config = new KubernetesConfig("image", createOptions, Option.None<AuthConfig>());
            var docker = new DockerModule("module1", "v1", ModuleStatus.Running, RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
            var module = new KubernetesModule(docker, config);
            var moduleLabels = new Dictionary<string, string> { { "Label1", "VaLue1" } };
            var mapper = new KubernetesServiceMapper(PortMapServiceType.ClusterIP);
            var moduleId = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "moduleid", Mock.Of<ICredentials>());

            var service = mapper.CreateService(moduleId, module, moduleLabels).OrDefault();

            Assert.Equal(1, service.Spec.Ports.Count);
            Assert.Equal(1, service.Metadata.Annotations.Count);
            Assert.NotNull(service.Metadata.Annotations[KubernetesConstants.CreationString]);
            Assert.Equal(1, service.Metadata.Labels.Count);
            Assert.Equal("VaLue1", service.Metadata.Labels["Label1"]);
            Assert.Equal("ClusterIP", service.Spec.Type);
            Assert.Equal(1, service.Spec.Selector.Count);
            Assert.Equal("VaLue1", service.Spec.Selector["Label1"]);
        }

        [Fact]
        public void PortBindingsCreatesAServiceWithPorts()
        {
            var hostConfig = new DockerModels.HostConfig
            {
                PortBindings = new Dictionary<string, IList<DockerModels.PortBinding>>
                {
                    ["10/TCP"] = new List<DockerModels.PortBinding> { new DockerModels.PortBinding { HostPort = "10" } }
                }
            };
            var createOptions = CreatePodParameters.Create(hostConfig: hostConfig);
            var config = new KubernetesConfig("image", createOptions, Option.None<AuthConfig>());
            var docker = new DockerModule("module1", "v1", ModuleStatus.Running, RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
            var module = new KubernetesModule(docker, config);
            var moduleLabels = new Dictionary<string, string>();
            var mapper = new KubernetesServiceMapper(PortMapServiceType.ClusterIP);
            var moduleId = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "moduleid", Mock.Of<ICredentials>());

            var service = mapper.CreateService(moduleId, module, moduleLabels).OrDefault();

            Assert.Equal(1, service.Spec.Ports.Count);
            AssertPort(new V1ServicePort(10, "hostport-10-tcp", null, "TCP", 10), service.Spec.Ports.First());
            Assert.Equal("ClusterIP", service.Spec.Type);
        }

        [Fact]
        public void ExposingPortsCreatesAServiceWithPorts()
        {
            var exposedPorts = new Dictionary<string, EmptyStruct> { ["10/TCP"] = default(EmptyStruct) };
            var createOptions = CreatePodParameters.Create(exposedPorts: exposedPorts);
            var config = new KubernetesConfig("image", createOptions, Option.None<AuthConfig>());
            var docker = new DockerModule("module1", "v1", ModuleStatus.Running, RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
            var module = new KubernetesModule(docker, config);
            var moduleLabels = new Dictionary<string, string>();
            var mapper = new KubernetesServiceMapper(PortMapServiceType.ClusterIP);
            var moduleId = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "moduleid", Mock.Of<ICredentials>());

            var service = mapper.CreateService(moduleId, module, moduleLabels).OrDefault();

            Assert.Equal(1, service.Spec.Ports.Count);
            AssertPort(new V1ServicePort(10, "exposedport-10-tcp", null, "TCP"), service.Spec.Ports.First());
            Assert.Equal("ClusterIP", service.Spec.Type);
        }

        [Fact]
        public void PortBindingOverrideExposedPort()
        {
            var exposedPorts = new Dictionary<string, EmptyStruct> { ["10/TCP"] = default(EmptyStruct) };
            var hostConfig = new DockerModels.HostConfig
            {
                PortBindings = new Dictionary<string, IList<DockerModels.PortBinding>>
                {
                    ["10/TCP"] = new List<DockerModels.PortBinding> { new DockerModels.PortBinding { HostPort = "10" } }
                }
            };
            var createOptions = CreatePodParameters.Create(exposedPorts: exposedPorts, hostConfig: hostConfig);
            var config = new KubernetesConfig("image", createOptions, Option.None<AuthConfig>());
            var docker = new DockerModule("module1", "v1", ModuleStatus.Running, RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVars);
            var module = new KubernetesModule(docker, config);
            var moduleLabels = new Dictionary<string, string>();
            var mapper = new KubernetesServiceMapper(PortMapServiceType.ClusterIP);
            var moduleId = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "moduleid", Mock.Of<ICredentials>());

            var service = mapper.CreateService(moduleId, module, moduleLabels).OrDefault();

            Assert.Equal(1, service.Spec.Ports.Count);
            AssertPort(new V1ServicePort(10, "hostport-10-tcp", null, "TCP", 10), service.Spec.Ports.First());
            Assert.Equal("ClusterIP", service.Spec.Type);
        }

        static void AssertPort(V1ServicePort expected, V1ServicePort actual)
        {
            Assert.NotNull(actual);
            Assert.Equal(expected.Name, actual.Name);
            Assert.Equal(expected.Port, actual.Port);
            Assert.Equal(expected.NodePort, actual.NodePort);
            Assert.Equal(expected.TargetPort, actual.TargetPort);
            Assert.Equal(expected.Protocol, actual.Protocol);
        }
    }
}
