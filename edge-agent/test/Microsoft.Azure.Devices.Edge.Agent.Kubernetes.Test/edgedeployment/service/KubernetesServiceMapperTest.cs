// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Test.EdgeDeployment.Service
{
    using System.Collections.Generic;
    using System.Linq;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Microsoft.Azure.Devices.Edge.Agent.Docker.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment.Service;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
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
            ["5000/udp"] = default(DockerEmptyStruct)
        };

        static readonly HostConfig HostPorts = new HostConfig
        {
            PortBindings = new Dictionary<string, IList<PortBinding>>
            {
                ["80/tcp"] = new List<PortBinding>
                    {
                        new PortBinding { HostPort = "8080" }
                    },
                ["5000/udp"] = new List<PortBinding>
                    {
                        new PortBinding { HostPort = "5050" }
                    }
            }
        };

        static readonly Dictionary<string, string> DefaultLabels = new Dictionary<string, string>
        {
            [KubernetesConstants.K8sEdgeDeviceLabel] = KubeUtils.SanitizeLabelValue("device1"),
        };

        static readonly ModuleIdentity CreateIdentity = new ModuleIdentity("hostname", "device1", "Module1", new ConnectionStringCredentials("connection string"));

        static KubernetesModule CreateKubernetesModule(CreatePodParameters podParameters)
        {
            var config = new KubernetesConfig("image", podParameters, Option.None<AuthConfig>());
            var docker = new DockerModule("module1", "v1", ModuleStatus.Running, RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, Constants.DefaultStartupOrder, DefaultConfigurationInfo, EnvVarsDict);
            var owner = new KubernetesModuleOwner("v1", "Owner", "an-owner", "a-uid");
            return new KubernetesModule(docker, config, owner);
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
        public void CreateServiceSetsServiceOptions()
        {
            var serviceOptions = new KubernetesServiceOptions("loadBalancerIP", "nodeport");
            var module = CreateKubernetesModule(CreatePodParameters.Create(exposedPorts: ExposedPorts, serviceOptions: serviceOptions));
            var mapper = new KubernetesServiceMapper(PortMapServiceType.ClusterIP);

            Option<V1Service> result = mapper.CreateService(CreateIdentity, module, DefaultLabels);

            Assert.True(result.HasValue);
            var service = result.OrDefault();
            Assert.Equal(PortMapServiceType.NodePort.ToString(), service.Spec.Type);
            Assert.Equal("loadBalancerIP", service.Spec.LoadBalancerIP);
        }

        [Fact]
        public void CreateServiceSetsServiceOptionsOverridesSetDefault()
        {
            var serviceOptions = new KubernetesServiceOptions("loadBalancerIP", "clusterIP");
            var module = CreateKubernetesModule(CreatePodParameters.Create(hostConfig: HostPorts, serviceOptions: serviceOptions));
            var mapper = new KubernetesServiceMapper(PortMapServiceType.LoadBalancer);

            Option<V1Service> result = mapper.CreateService(CreateIdentity, module, DefaultLabels);

            Assert.True(result.HasValue);
            var service = result.OrDefault();
            Assert.Equal(PortMapServiceType.ClusterIP.ToString(), service.Spec.Type);
            Assert.Equal("loadBalancerIP", service.Spec.LoadBalancerIP);
        }

        [Fact]
        public void CreateServiceSetsServiceOptionsNoIPOnNullLoadBalancerIP()
        {
            var serviceOptions = new KubernetesServiceOptions(null, "loadBalancer");
            var module = CreateKubernetesModule(CreatePodParameters.Create(exposedPorts: ExposedPorts, serviceOptions: serviceOptions));
            var mapper = new KubernetesServiceMapper(PortMapServiceType.ClusterIP);

            Option<V1Service> result = mapper.CreateService(CreateIdentity, module, DefaultLabels);

            Assert.True(result.HasValue);
            var service = result.OrDefault();
            Assert.Equal(PortMapServiceType.LoadBalancer.ToString(), service.Spec.Type);
            Assert.Null(service.Spec.LoadBalancerIP);
        }

        [Fact]
        public void CreateServiceSetsServiceOptionsSetDefaultOnNullType()
        {
            var serviceOptions = new KubernetesServiceOptions("loadBalancerIP", null);
            var module = CreateKubernetesModule(CreatePodParameters.Create(hostConfig: HostPorts, serviceOptions: serviceOptions));
            var mapper = new KubernetesServiceMapper(PortMapServiceType.LoadBalancer);

            Option<V1Service> result = mapper.CreateService(CreateIdentity, module, DefaultLabels);

            Assert.True(result.HasValue);
            var service = result.OrDefault();
            Assert.Equal(PortMapServiceType.LoadBalancer.ToString(), service.Spec.Type);
            Assert.Equal("loadBalancerIP", service.Spec.LoadBalancerIP);
        }

        [Fact]
        public void CreateServiceExposedPortsOnlyCreatesExposedPortService()
        {
            var module = CreateKubernetesModule(CreatePodParameters.Create(exposedPorts: ExposedPorts));
            var mapper = new KubernetesServiceMapper(PortMapServiceType.LoadBalancer);
            Option<V1Service> result = mapper.CreateService(CreateIdentity, module, DefaultLabels);
            Assert.True(result.HasValue);
            var service = result.OrDefault();
            V1ServicePort port80 = service.Spec.Ports.Single(p => p.Port == 80);
            Assert.Equal("exposedport-80-tcp", port80.Name);
            Assert.Equal("TCP", port80.Protocol);
            V1ServicePort port5000 = service.Spec.Ports.Single(p => p.Port == 5000);
            Assert.Equal("exposedport-5000-udp", port5000.Name);
            Assert.Equal("UDP", port5000.Protocol);
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
        public void CreateServiceHostPortsCreatesHostportService()
        {
            var module = CreateKubernetesModule(CreatePodParameters.Create(hostConfig: HostPorts));
            var mapper = new KubernetesServiceMapper(PortMapServiceType.LoadBalancer);
            Option<V1Service> result = mapper.CreateService(CreateIdentity, module, DefaultLabels);
            Assert.True(result.HasValue);
            var service = result.OrDefault();
            V1ServicePort port80 = service.Spec.Ports.Single(p => p.Port == 8080);
            Assert.Equal("hostport-80-tcp", port80.Name);
            Assert.Equal("TCP", port80.Protocol);
            Assert.Equal(80, (int)port80.TargetPort);
            V1ServicePort port5000 = service.Spec.Ports.Single(p => p.Port == 5050);
            Assert.Equal("hostport-5000-udp", port5000.Name);
            Assert.Equal("UDP", port5000.Protocol);
            Assert.Equal(5000, (int)port5000.TargetPort);
        }

        [Fact]
        public void CreateServiceExposedAndHostPortsCreatesHostportService()
        {
            var module = CreateKubernetesModule(CreatePodParameters.Create(exposedPorts: ExposedPorts, hostConfig: HostPorts));
            var mapper = new KubernetesServiceMapper(PortMapServiceType.LoadBalancer);
            Option<V1Service> result = mapper.CreateService(CreateIdentity, module, DefaultLabels);
            Assert.True(result.HasValue);
            var service = result.OrDefault();
            Assert.Equal(PortMapServiceType.LoadBalancer.ToString(), service.Spec.Type);
            V1ServicePort port80 = service.Spec.Ports.Single(p => p.Port == 8080);
            Assert.Equal("hostport-80-tcp", port80.Name);
            Assert.Equal("TCP", port80.Protocol);
            Assert.Equal(80, (int)port80.TargetPort);
            V1ServicePort port5000 = service.Spec.Ports.Single(p => p.Port == 5050);
            Assert.Equal("hostport-5000-udp", port5000.Name);
            Assert.Equal("UDP", port5000.Protocol);
            Assert.Equal(5000, (int)port5000.TargetPort);
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

        [Fact]
        public void ServiceAnnotationsAreLabels()
        {
            var dockerLabels = new Dictionary<string, string>
            {
                ["Complicated Value that doesn't fit in k8s label name"] = "Complicated Value that doesn't fit in k8s label value",
                ["Label2"] = "Value2"
            };
            var module = CreateKubernetesModule(CreatePodParameters.Create(exposedPorts: ExposedPorts, hostConfig: HostPorts, labels: dockerLabels));
            var mapper = new KubernetesServiceMapper(PortMapServiceType.LoadBalancer);
            Option<V1Service> result = mapper.CreateService(CreateIdentity, module, DefaultLabels);

            Assert.True(result.HasValue);
            var service = result.OrDefault();
            Assert.Equal("Complicated Value that doesn't fit in k8s label value", service.Metadata.Annotations["ComplicatedValuethatdoesntfitink8slabelname"]);
            Assert.Equal("Value2", service.Metadata.Annotations["Label2"]);
        }
    }
}
