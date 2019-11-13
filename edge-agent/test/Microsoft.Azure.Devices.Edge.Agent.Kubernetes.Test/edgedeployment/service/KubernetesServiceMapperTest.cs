// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Test.Edgedeployment.Service
{
    using System;
    using System.Collections.Generic;
    using System.Security.Authentication.ExtendedProtection;
    using System.Text;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Microsoft.Azure.Devices.Edge.Agent.Docker.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment.Service;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    using DockerEmptyStruct = global::Docker.DotNet.Models.EmptyStruct;
    using KubernetesConstants = Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Constants;

    [Unit]
    public class KubernetesServiceMapperTest
    {
        static readonly ConfigurationInfo DefaultConfigurationInfo = new ConfigurationInfo("1");

        static readonly IDictionary<string, EnvVal> EnvVarsDict = new Dictionary<string, EnvVal>();

        static readonly DockerConfig Config1 = new DockerConfig("test-image:1");

        static readonly Dictionary<string, DockerEmptyStruct> ExposedPorts = new Dictionary<string, DockerEmptyStruct>
            {
                ["80/tcp"] = default(DockerEmptyStruct),
                ["5000/udp"] = default(DockerEmptyStruct),
            };

        static readonly HostConfig HostPorts = new HostConfig
        {
            PortBindings = new Dictionary<string, IList<PortBinding>>
            {
                ["80/tcp"] = new List<PortBinding>
                    {
                        new PortBinding() { HostPort = "8080" },
                    },
                ["5000/udp"] = new List<PortBinding>
                    {
                        new PortBinding() { HostPort = "5050" },
                    },
            }
        };

        static readonly Dictionary<string, string> DefaultLabels = new Dictionary<string, string>
        {
            [KubernetesConstants.K8sEdgeDeviceLabel] = KubeUtils.SanitizeLabelValue("device1"),
            [KubernetesConstants.K8sEdgeHubNameLabel] = KubeUtils.SanitizeLabelValue("hostname")
        };

        static readonly ModuleIdentity CreateIdentity = new ModuleIdentity("hostname", "gateway", "device1", "Module1", new ConnectionStringCredentials("connection string"));

        static KubernetesModule CreateKubernetesModule(CreatePodParameters podParameters)
        {
            var config = new KubernetesConfig("image", podParameters, Option.None<AuthConfig>());
            var docker = new DockerModule("module1", "v1", ModuleStatus.Running, RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVarsDict);
            return new KubernetesModule(docker, config);
        }

        [Fact]
        public void NoPortInOptionsCreatesNoService()
        {
            var module = CreateKubernetesModule(CreatePodParameters.Create());
            var mapper = new KubernetesServiceMapper(PortMapServiceType.LoadBalancer);
            Option<V1Service> result = mapper.CreateService(CreateIdentity, module, DefaultLabels);
            Assert.False(result.HasValue);
        }

        [Fact]
        public void CreateServiceExposedPortsOnlyCreatesClusterIP()
        {
            var module = CreateKubernetesModule(CreatePodParameters.Create(exposedPorts: ExposedPorts));
            var mapper = new KubernetesServiceMapper(PortMapServiceType.LoadBalancer);
            Option<V1Service> result = mapper.CreateService(CreateIdentity, module, DefaultLabels);
            Assert.True(result.HasValue);
            var service = result.OrDefault();
            Assert.Equal(PortMapServiceType.ClusterIP.ToString(), service.Spec.Type);
        }

        [Fact]
        public void CreateServiceHostPortsCreatesDefaultServiceType()
        {
            var module = CreateKubernetesModule(CreatePodParameters.Create(hostConfig: HostPorts));
            var mapper = new KubernetesServiceMapper(PortMapServiceType.LoadBalancer);
            Option<V1Service> result = mapper.CreateService(CreateIdentity, module, DefaultLabels);

            Assert.True(result.HasValue);
            var service = result.OrDefault();
            Assert.Equal(PortMapServiceType.LoadBalancer.ToString(), service.Spec.Type);
        }

        [Fact]
        public void ServiceNameIsModuleName()
        {
            var module = CreateKubernetesModule(CreatePodParameters.Create(exposedPorts: ExposedPorts, hostConfig: HostPorts));
            var mapper = new KubernetesServiceMapper(PortMapServiceType.LoadBalancer);
            Option<V1Service> result = mapper.CreateService(CreateIdentity, module, DefaultLabels);

            Assert.True(result.HasValue);
            var service = result.OrDefault();
            Assert.Equal("module1", service.Metadata.Name);
        }
    }
}
