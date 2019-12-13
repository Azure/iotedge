// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Microsoft.Azure.Devices.Edge.Agent.Docker.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment.Deployment;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment.Service;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Newtonsoft.Json.Linq;
    using Xunit;
    using KubernetesConstants = Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Constants;

    [Unit]
    public class KubernetesDeploymentMapperTest
    {
        static readonly ConfigurationInfo DefaultConfigurationInfo = new ConfigurationInfo("1");

        static readonly IDictionary<string, EnvVal> EnvVarsDict = new Dictionary<string, EnvVal>();

        static readonly DockerConfig Config1 = new DockerConfig("test-image:1");

        static readonly HostConfig VolumeMountHostConfig = new HostConfig
        {
            Mounts = new List<Mount>
            {
                new Mount
                {
                    Type = "volume",
                    ReadOnly = true,
                    Source = "a-volume",
                    Target = "/tmp/volume"
                }
            }
        };

        static readonly KubernetesModuleOwner EdgeletModuleOwner = new KubernetesModuleOwner("v1", "Deployment", "iotedged", "123");

        [Fact]
        public void EmptyIsNotAllowedAsPodAnnotation()
        {
            var identity = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "Module1", Mock.Of<ICredentials>());
            var labels = new Dictionary<string, string>
            {
                // string.Empty is an invalid label name
                { string.Empty, "test" }
            };
            var config = new KubernetesConfig("image", CreatePodParameters.Create(labels: labels), Option.None<AuthConfig>());
            var docker = new DockerModule("module1", "v1", ModuleStatus.Running, RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVarsDict);
            var module = new KubernetesModule(docker, config, EdgeletModuleOwner);
            var moduleLabels = new Dictionary<string, string>();
            var mapper = CreateMapper();

            Assert.Throws<InvalidKubernetesNameException>(() => mapper.CreateDeployment(identity, module, moduleLabels));
        }

        [Fact]
        public void SimpleDeploymentCreationHappyPath()
        {
            var identity = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "Module1", Mock.Of<ICredentials>());
            var config = new KubernetesConfig("image", CreatePodParameters.Create(), Option.None<AuthConfig>());
            var docker = new DockerModule("module1", "v1", ModuleStatus.Running, RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVarsDict);
            var module = new KubernetesModule(docker, config, EdgeletModuleOwner);
            var labels = new Dictionary<string, string>();
            var mapper = CreateMapper();

            var deployment = mapper.CreateDeployment(identity, module, labels);

            Assert.NotNull(deployment);
            Assert.Equal(1, deployment.Metadata.OwnerReferences.Count);
            Assert.Equal(V1Deployment.KubeKind, deployment.Metadata.OwnerReferences[0].Kind);
            Assert.Equal(EdgeletModuleOwner.Name, deployment.Metadata.OwnerReferences[0].Name);
            Assert.Equal(1, deployment.Spec.Replicas);
        }

        [Fact]
        public void SimpleDeploymentStoppedHasZeroReplicas()
        {
            var identity = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "Module1", Mock.Of<ICredentials>());
            var config = new KubernetesConfig("image", CreatePodParameters.Create(), Option.None<AuthConfig>());
            var docker = new DockerModule("module1", "v1", ModuleStatus.Stopped, RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVarsDict);
            var module = new KubernetesModule(docker, config, EdgeletModuleOwner);
            var labels = new Dictionary<string, string>();
            var mapper = CreateMapper();

            var deployment = mapper.CreateDeployment(identity, module, labels);

            Assert.NotNull(deployment);
            Assert.Equal(1, deployment.Metadata.OwnerReferences.Count);
            Assert.Equal(V1Deployment.KubeKind, deployment.Metadata.OwnerReferences[0].Kind);
            Assert.Equal(EdgeletModuleOwner.Name, deployment.Metadata.OwnerReferences[0].Name);
            Assert.Equal(0, deployment.Spec.Replicas);
        }

        [Fact]
        public void ValidatePodPropertyTranslation()
        {
            var identity = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "Module1", Mock.Of<ICredentials>());
            var labels = new Dictionary<string, string>
            {
                // Add a label
                { "demo", "test" }
            };
            var hostConfig = new HostConfig
            {
                // Make container privileged
                Privileged = true,
                // Add a readonly mount
                Binds = new List<string> { "/home/blah:/home/blah2:ro" }
            };
            var config = new KubernetesConfig("image", CreatePodParameters.Create(labels: labels, hostConfig: hostConfig), Option.None<AuthConfig>());
            var docker = new DockerModule("module1", "v1", ModuleStatus.Running, RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVarsDict);
            var module = new KubernetesModule(docker, config, EdgeletModuleOwner);
            var moduleLabels = new Dictionary<string, string>();
            var mapper = CreateMapper();

            var deployment = mapper.CreateDeployment(identity, module, moduleLabels);
            var pod = deployment.Spec.Template;

            Assert.NotNull(pod);
            // Validate annotation
            Assert.True(pod.Metadata.Annotations.ContainsKey("demo"));
            // Two containers should exist - proxy and the module
            Assert.Equal(2, pod.Spec.Containers.Count);

            // There should only be one module container
            var moduleContainer = pod.Spec.Containers.Single(p => p.Name != "proxy");
            // We made this container privileged
            Assert.True(moduleContainer.SecurityContext.Privileged);
            // Validate that there are 1 mounts for module container
            Assert.Equal(1, moduleContainer.VolumeMounts.Count);
            // Validate the custom mount that we added
            Assert.Contains(moduleContainer.VolumeMounts, vm => vm.Name.Equals("homeblah"));
            var mount = moduleContainer.VolumeMounts.Single(vm => vm.Name.Equals("homeblah"));
            // Lets make sure that it is read only
            Assert.True(mount.ReadOnlyProperty);

            // Validate proxy container
            var proxyContainer = pod.Spec.Containers.Single(p => p.Name == "proxy");
            // Validate that there are 2 mounts for proxy container: config and trust-bundle
            Assert.Equal(2, proxyContainer.VolumeMounts.Count);
            Assert.Contains(proxyContainer.VolumeMounts, vm => vm.Name.Equals("configVolumeName"));
            Assert.Contains(proxyContainer.VolumeMounts, vm => vm.Name.Equals("trustBundleVolumeName"));

            // Validate pod volumes
            Assert.Equal(3, pod.Spec.Volumes.Count);
            Assert.Contains(pod.Spec.Volumes, v => v.Name.Equals("homeblah"));
            Assert.Contains(pod.Spec.Volumes, v => v.Name.Equals("configVolumeName"));
            Assert.Contains(pod.Spec.Volumes, v => v.Name.Equals("trustBundleVolumeName"));

            // Validate no image pull secrets for public images
            Assert.Null(pod.Spec.ImagePullSecrets);

            // Validate null pod security context by default
            Assert.Null(pod.Spec.SecurityContext);
        }

        [Fact]
        public void EmptyDirMappingForVolume()
        {
            var identity = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "ModuleId", Mock.Of<ICredentials>());
            var labels = new Dictionary<string, string>();
            var hostConfig = VolumeMountHostConfig;
            var config = new KubernetesConfig("image", CreatePodParameters.Create(labels: labels, hostConfig: hostConfig), Option.None<AuthConfig>());
            var docker = new DockerModule("module1", "v1", ModuleStatus.Running, RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVarsDict);
            var module = new KubernetesModule(docker, config, EdgeletModuleOwner);
            var mapper = CreateMapper(storageClassName: null);
            var deployment = mapper.CreateDeployment(identity, module, labels);
            var pod = deployment.Spec.Template;

            Assert.True(pod != null);
            var podVolume = pod.Spec.Volumes.Single(v => v.Name == "a-volume");
            Assert.NotNull(podVolume.EmptyDir);
            var podVolumeMount = pod.Spec.Containers.Single(p => p.Name != "proxy").VolumeMounts.Single(vm => vm.Name == "a-volume");
            Assert.Equal("/tmp/volume", podVolumeMount.MountPath);
            Assert.True(podVolumeMount.ReadOnlyProperty);
        }

        [Fact]
        public void InvalidPvcMappingForVolumeNameVolume()
        {
            var identity = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "ModuleId", Mock.Of<ICredentials>());
            var labels = new Dictionary<string, string>();
            var hostConfig = VolumeMountHostConfig;
            var config = new KubernetesConfig("image", CreatePodParameters.Create(labels: labels, hostConfig: hostConfig), Option.None<AuthConfig>());
            var docker = new DockerModule("module1", "v1", ModuleStatus.Running, RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVarsDict);
            var module = new KubernetesModule(docker, config, EdgeletModuleOwner);
            var mapper = CreateMapper("elephant", null);

            Assert.Throws<InvalidModuleException>(() => mapper.CreateDeployment(identity, module, labels));
        }

        [Fact]
        public void PvcMappingForVolumeNameVolume()
        {
            var identity = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "ModuleId", Mock.Of<ICredentials>());
            var labels = new Dictionary<string, string>();
            var hostConfig = VolumeMountHostConfig;
            var config = new KubernetesConfig("image", CreatePodParameters.Create(labels: labels, hostConfig: hostConfig), Option.None<AuthConfig>());
            var docker = new DockerModule("module1", "v1", ModuleStatus.Running, RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVarsDict);
            var module = new KubernetesModule(docker, config, EdgeletModuleOwner);
            var mapper = CreateMapper("a-volume", null);

            var deployment = mapper.CreateDeployment(identity, module, labels);

            var pod = deployment.Spec.Template;

            Assert.True(pod != null);
            var podVolume = pod.Spec.Volumes.Single(v => v.Name == "a-volume");
            Assert.NotNull(podVolume.PersistentVolumeClaim);

            Assert.Equal("module1-a-volume", podVolume.PersistentVolumeClaim.ClaimName);
            Assert.True(podVolume.PersistentVolumeClaim.ReadOnlyProperty);
            var podVolumeMount = pod.Spec.Containers.Single(p => p.Name != "proxy").VolumeMounts.Single(vm => vm.Name == "a-volume");
            Assert.Equal("/tmp/volume", podVolumeMount.MountPath);
            Assert.True(podVolumeMount.ReadOnlyProperty);
        }

        [Fact]
        public void PvcMappingForStorageClassVolume()
        {
            var identity = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "ModuleId", Mock.Of<ICredentials>());
            var labels = new Dictionary<string, string>();
            var hostConfig = VolumeMountHostConfig;
            var config = new KubernetesConfig("image", CreatePodParameters.Create(labels: labels, hostConfig: hostConfig), Option.None<AuthConfig>());
            var docker = new DockerModule("module1", "v1", ModuleStatus.Running, RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVarsDict);
            var module = new KubernetesModule(docker, config, EdgeletModuleOwner);
            var mapper = CreateMapper();

            var deployment = mapper.CreateDeployment(identity, module, labels);
            var pod = deployment.Spec.Template;

            Assert.True(pod != null);
            var podVolume = pod.Spec.Volumes.Single(v => v.Name == "a-volume");
            Assert.NotNull(podVolume.PersistentVolumeClaim);
            Assert.Equal("module1-a-volume", podVolume.PersistentVolumeClaim.ClaimName);
            Assert.True(podVolume.PersistentVolumeClaim.ReadOnlyProperty);
            var podVolumeMount = pod.Spec.Containers.Single(p => p.Name != "proxy").VolumeMounts.Single(vm => vm.Name == "a-volume");
            Assert.Equal("/tmp/volume", podVolumeMount.MountPath);
            Assert.True(podVolumeMount.ReadOnlyProperty);
        }

        [Fact]
        public void PvcMappingForDefaultStorageClassVolume()
        {
            var identity = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "ModuleId", Mock.Of<ICredentials>());
            var labels = new Dictionary<string, string>();
            var hostConfig = VolumeMountHostConfig;
            var config = new KubernetesConfig("image", CreatePodParameters.Create(labels: labels, hostConfig: hostConfig), Option.None<AuthConfig>());
            var docker = new DockerModule("module1", "v1", ModuleStatus.Running, RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVarsDict);
            var module = new KubernetesModule(docker, config, EdgeletModuleOwner);
            var mapper = CreateMapper();

            var deployment = mapper.CreateDeployment(identity, module, labels);
            var pod = deployment.Spec.Template;

            Assert.True(pod != null);
            var podVolume = pod.Spec.Volumes.Single(v => v.Name == "a-volume");
            Assert.NotNull(podVolume.PersistentVolumeClaim);
            Assert.Equal("module1-a-volume", podVolume.PersistentVolumeClaim.ClaimName);
            Assert.True(podVolume.PersistentVolumeClaim.ReadOnlyProperty);
            var podVolumeMount = pod.Spec.Containers.Single(p => p.Name != "proxy").VolumeMounts.Single(vm => vm.Name == "a-volume");
            Assert.Equal("/tmp/volume", podVolumeMount.MountPath);
            Assert.True(podVolumeMount.ReadOnlyProperty);
        }

        [Fact]
        public void PvcMappingForPVVolumeExtended()
        {
            var identity = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "ModuleId", Mock.Of<ICredentials>());
            var labels = new Dictionary<string, string>();
            var hostConfig = VolumeMountHostConfig;

            var experimental = new Dictionary<string, JToken>
            {
                ["k8s-experimental"] = JToken.Parse(
                    @"{ 
                        ""volumes"": [
                          {
                            ""volume"": {
                              ""name"": ""module-config"",
                            },
                         
                          ""volumeMounts"": [
                            {
                              ""name"": ""module-config"",
                              ""mountPath"": ""/etc/module"",
                              ""mountPropagation"": ""None"",
                              ""readOnly"": ""true"",
                              ""subPath"": """" 
                            }
                          ]
                        }  
                    ]}")
            };

            var parameters = KubernetesExperimentalCreatePodParameters.Parse(experimental).OrDefault();

            var volumes = new[]
            {
                parameters.Volumes.OrDefault().Single(),
            };

            var config = new KubernetesConfig("image", CreatePodParameters.Create(volumes: volumes, hostConfig: hostConfig), Option.None<AuthConfig>());
            var docker = new DockerModule("module1", "v1", ModuleStatus.Running, RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVarsDict);
            var module = new KubernetesModule(docker, config, EdgeletModuleOwner);
            var mapper = CreateMapper();

            var deployment = mapper.CreateDeployment(identity, module, labels);
            var pod = deployment.Spec.Template;

            Assert.True(pod != null);
            var podVolume = pod.Spec.Volumes.Single(v => v.Name == "module-config");
            var podVolumeMount = pod.Spec.Containers.Single(p => p.Name != "proxy").VolumeMounts.Single(vm => vm.Name == "module-config");
            Assert.Equal("/etc/module", podVolumeMount.MountPath);
        }

        [Fact]
        public void AppliesNodeSelectorFromCreateOptionsToPodSpec()
        {
            var identity = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "Module1", Mock.Of<ICredentials>());
            var docker = new DockerModule("module1", "v1", ModuleStatus.Running, RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVarsDict);
            IDictionary<string, string> nodeSelector = new Dictionary<string, string>
            {
                ["disktype"] = "ssd"
            };
            var config = new KubernetesConfig("image", CreatePodParameters.Create(nodeSelector: nodeSelector), Option.None<AuthConfig>());
            var module = new KubernetesModule(docker, config, EdgeletModuleOwner);
            var labels = new Dictionary<string, string>();
            var mapper = CreateMapper();

            var deployment = mapper.CreateDeployment(identity, module, labels);

            Assert.Equal(nodeSelector, deployment.Spec.Template.Spec.NodeSelector, new DictionaryComparer<string, string>());
        }

        [Fact]
        public void LeaveNodeSelectorEmptyWhenNothingProvidedInCreateOptions()
        {
            var identity = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "Module1", Mock.Of<ICredentials>());
            var docker = new DockerModule("module1", "v1", ModuleStatus.Running, RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVarsDict);
            var config = new KubernetesConfig("image", CreatePodParameters.Create(), Option.None<AuthConfig>());
            var module = new KubernetesModule(docker, config, EdgeletModuleOwner);
            var labels = new Dictionary<string, string>();
            var mapper = CreateMapper();

            var deployment = mapper.CreateDeployment(identity, module, labels);

            Assert.Null(deployment.Spec.Template.Spec.NodeSelector);
        }

        [Fact]
        public void AppliesResourcesFromCreateOptionsToContainerSpec()
        {
            var identity = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "Module1", Mock.Of<ICredentials>());
            var docker = new DockerModule("module1", "v1", ModuleStatus.Running, RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVarsDict);
            var resources = new V1ResourceRequirements(
                new Dictionary<string, ResourceQuantity>
                {
                    ["memory"] = new ResourceQuantity("128Mi"),
                    ["cpu"] = new ResourceQuantity("500M"),
                    ["hardware-vendor.example/foo"] = 2
                },
                new Dictionary<string, ResourceQuantity>
                {
                    ["memory"] = new ResourceQuantity("64Mi"),
                    ["cpu"] = new ResourceQuantity("250M"),
                    ["hardware-vendor.example/foo"] = 1
                });
            var config = new KubernetesConfig("image", CreatePodParameters.Create(resources: resources), Option.None<AuthConfig>());
            var module = new KubernetesModule(docker, config, EdgeletModuleOwner);
            var labels = new Dictionary<string, string>();
            var mapper = CreateMapper();

            var deployment = mapper.CreateDeployment(identity, module, labels);

            var moduleContainer = deployment.Spec.Template.Spec.Containers.Single(container => container.Name == "module1");
            Assert.Equal(resources.Limits, moduleContainer.Resources.Limits);
            Assert.Equal(resources.Requests, moduleContainer.Resources.Requests);
        }

        [Fact]
        public void LeaveResourcesEmptyWhenNothingProvidedInCreateOptions()
        {
            var identity = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "Module1", Mock.Of<ICredentials>());
            var docker = new DockerModule("module1", "v1", ModuleStatus.Running, RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVarsDict);
            var config = new KubernetesConfig("image", CreatePodParameters.Create(), Option.None<AuthConfig>());
            var module = new KubernetesModule(docker, config, EdgeletModuleOwner);
            var labels = new Dictionary<string, string>();
            var mapper = CreateMapper();

            var deployment = mapper.CreateDeployment(identity, module, labels);

            var moduleContainer = deployment.Spec.Template.Spec.Containers.Single(container => container.Name == "module1");
            Assert.Null(moduleContainer.Resources);
        }

        [Fact]
        public void AppliesVolumesFromCreateOptionsToContainerSpec()
        {
            var identity = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "Module1", Mock.Of<ICredentials>());
            var docker = new DockerModule("module1", "v1", ModuleStatus.Running, RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVarsDict);
            var volumes = new[]
            {
                new KubernetesModuleVolumeSpec(
                    new V1Volume("additional-volume", configMap: new V1ConfigMapVolumeSource(name: "additional-config-map")),
                    new[] { new V1VolumeMount(name: "additional-volume", mountPath: "/etc") })
            };
            var config = new KubernetesConfig("image", CreatePodParameters.Create(volumes: volumes), Option.None<AuthConfig>());
            var module = new KubernetesModule(docker, config, EdgeletModuleOwner);
            var labels = new Dictionary<string, string>();
            var mapper = CreateMapper();

            var deployment = mapper.CreateDeployment(identity, module, labels);

            // Validate module volume mounts
            var moduleContainer = deployment.Spec.Template.Spec.Containers.Single(container => container.Name == "module1");
            Assert.Equal(1, moduleContainer.VolumeMounts.Count);
            Assert.Contains(moduleContainer.VolumeMounts, vm => vm.Name.Equals("additional-volume"));

            // Validate proxy volume mounts
            var proxyContainer = deployment.Spec.Template.Spec.Containers.Single(p => p.Name == "proxy");
            Assert.Equal(2, proxyContainer.VolumeMounts.Count);
            Assert.Contains(proxyContainer.VolumeMounts, vm => vm.Name.Equals("configVolumeName"));
            Assert.Contains(proxyContainer.VolumeMounts, vm => vm.Name.Equals("trustBundleVolumeName"));

            // Validate pod volumes
            Assert.Equal(3, deployment.Spec.Template.Spec.Volumes.Count);
            Assert.Contains(deployment.Spec.Template.Spec.Volumes, v => v.Name.Equals("additional-volume"));
            Assert.Contains(deployment.Spec.Template.Spec.Volumes, v => v.Name.Equals("configVolumeName"));
            Assert.Contains(deployment.Spec.Template.Spec.Volumes, v => v.Name.Equals("trustBundleVolumeName"));
        }

        [Fact]
        public void AddsVolumesFromCreateOptionsToContainerSpecEvenIfTheyOverrideExistingOnes()
        {
            var identity = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "Module1", Mock.Of<ICredentials>());
            var docker = new DockerModule("module1", "v1", ModuleStatus.Running, RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVarsDict);
            var volumes = new[]
            {
                new KubernetesModuleVolumeSpec(
                    new V1Volume("homeblah", configMap: new V1ConfigMapVolumeSource(name: "additional-config-map")),
                    new[] { new V1VolumeMount(name: "homeblah", mountPath: "/home/blah") })
            };
            var hostConfig = new HostConfig { Binds = new List<string> { "/home/blah:/home/blah2:ro" } };
            var config = new KubernetesConfig("image", CreatePodParameters.Create(volumes: volumes, hostConfig: hostConfig), Option.None<AuthConfig>());
            var module = new KubernetesModule(docker, config, EdgeletModuleOwner);
            var labels = new Dictionary<string, string>();
            var mapper = CreateMapper();

            var deployment = mapper.CreateDeployment(identity, module, labels);

            // Validate module volume mounts
            var moduleContainer = deployment.Spec.Template.Spec.Containers.Single(container => container.Name == "module1");
            Assert.Equal(2, moduleContainer.VolumeMounts.Count(vm => vm.Name.Equals("homeblah")));

            // Validate proxy volume mounts
            var proxyContainer = deployment.Spec.Template.Spec.Containers.Single(p => p.Name == "proxy");
            Assert.Equal(2, proxyContainer.VolumeMounts.Count);
            Assert.Contains(proxyContainer.VolumeMounts, vm => vm.Name.Equals("configVolumeName"));
            Assert.Contains(proxyContainer.VolumeMounts, vm => vm.Name.Equals("trustBundleVolumeName"));

            // Validate pod volumes
            Assert.Equal(4, deployment.Spec.Template.Spec.Volumes.Count);
            Assert.Equal(2, deployment.Spec.Template.Spec.Volumes.Count(v => v.Name.Equals("homeblah")));
            Assert.Contains(deployment.Spec.Template.Spec.Volumes, v => v.Name.Equals("configVolumeName"));
            Assert.Contains(deployment.Spec.Template.Spec.Volumes, v => v.Name.Equals("trustBundleVolumeName"));
        }

        [Fact]
        public void LeaveVolumesIntactWhenNothingProvidedInCreateOptions()
        {
            var identity = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "Module1", Mock.Of<ICredentials>());
            var docker = new DockerModule("module1", "v1", ModuleStatus.Running, RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVarsDict);
            var config = new KubernetesConfig("image", CreatePodParameters.Create(), Option.None<AuthConfig>());
            var module = new KubernetesModule(docker, config, EdgeletModuleOwner);
            var labels = new Dictionary<string, string>();
            var mapper = CreateMapper();

            var deployment = mapper.CreateDeployment(identity, module, labels);

            // 2 volumes for proxy by default
            Assert.Equal(2, deployment.Spec.Template.Spec.Volumes.Count);
            var moduleContainer = deployment.Spec.Template.Spec.Containers.Single(container => container.Name == "module1");
            Assert.Equal(0, moduleContainer.VolumeMounts.Count);
            var proxyContainer = deployment.Spec.Template.Spec.Containers.Single(container => container.Name == "proxy");
            Assert.Equal(2, proxyContainer.VolumeMounts.Count);
        }

        [Fact]
        public void PassImagePullSecretsInPodSpecForProxyAndModuleContainers()
        {
            var identity = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "Module1", Mock.Of<ICredentials>());
            var config = new KubernetesConfig("image", CreatePodParameters.Create(), Option.Some(new AuthConfig("user-registry1")));
            var module = new KubernetesModule("module1", "v1", "docker", ModuleStatus.Running, RestartPolicy.Always, DefaultConfigurationInfo, EnvVarsDict, config, ImagePullPolicy.OnCreate, EdgeletModuleOwner);
            var labels = new Dictionary<string, string>();
            var mapper = CreateMapper(proxyImagePullSecretName: "user-registry2");

            var deployment = mapper.CreateDeployment(identity, module, labels);

            Assert.Equal(2, deployment.Spec.Template.Spec.ImagePullSecrets.Count);
            Assert.Contains(deployment.Spec.Template.Spec.ImagePullSecrets, secret => secret.Name == "user-registry1");
            Assert.Contains(deployment.Spec.Template.Spec.ImagePullSecrets, secret => secret.Name == "user-registry2");
        }

        [Fact]
        public void PassOnlyOneImagePullSecretInPodSpecIfProxyAndModuleContainersHasTheSameImagePullSecrets()
        {
            var identity = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "Module1", Mock.Of<ICredentials>());
            var config = new KubernetesConfig("image", CreatePodParameters.Create(), Option.Some(new AuthConfig("user-registry1")));
            var module = new KubernetesModule("module1", "v1", "docker", ModuleStatus.Running, RestartPolicy.Always, DefaultConfigurationInfo, EnvVarsDict, config, ImagePullPolicy.OnCreate, EdgeletModuleOwner);
            var labels = new Dictionary<string, string>();
            var mapper = CreateMapper(proxyImagePullSecretName: "user-registry1");

            var deployment = mapper.CreateDeployment(identity, module, labels);

            Assert.Equal(1, deployment.Spec.Template.Spec.ImagePullSecrets.Count);
            Assert.Contains(deployment.Spec.Template.Spec.ImagePullSecrets, secret => secret.Name == "user-registry1");
        }

        [Fact]
        public void RunAsNonRootAndRunAsUser1000SecurityPolicyWhenSettingSet()
        {
            var identity = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "Module1", Mock.Of<ICredentials>());
            var config = new KubernetesConfig("image", CreatePodParameters.Create(), Option.Some(new AuthConfig("user-registry1")));
            var module = new KubernetesModule("module1", "v1", "docker", ModuleStatus.Running, RestartPolicy.Always, DefaultConfigurationInfo, EnvVarsDict, config, ImagePullPolicy.OnCreate, EdgeletModuleOwner);
            var labels = new Dictionary<string, string>();
            var mapper = CreateMapper(runAsNonRoot: true);

            var deployment = mapper.CreateDeployment(identity, module, labels);

            Assert.Equal(1, deployment.Spec.Template.Spec.ImagePullSecrets.Count);
            Assert.Equal(true, deployment.Spec.Template.Spec.SecurityContext.RunAsNonRoot);
            Assert.Equal(1000, deployment.Spec.Template.Spec.SecurityContext.RunAsUser);
        }

        [Fact]
        public void PodSecurityContextFromCreateOptionsOverridesDefaultRunAsNonRootOptionsWhenProvided()
        {
            var identity = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "Module1", Mock.Of<ICredentials>());
            var securityContext = new V1PodSecurityContext { RunAsNonRoot = true, RunAsUser = 0 };
            var config = new KubernetesConfig("image", CreatePodParameters.Create(securityContext: securityContext), Option.Some(new AuthConfig("user-registry1")));
            var module = new KubernetesModule("module1", "v1", "docker", ModuleStatus.Running, RestartPolicy.Always, DefaultConfigurationInfo, EnvVarsDict, config, ImagePullPolicy.OnCreate, EdgeletModuleOwner);
            var labels = new Dictionary<string, string>();
            var mapper = CreateMapper(runAsNonRoot: true);

            var deployment = mapper.CreateDeployment(identity, module, labels);

            Assert.Equal(1, deployment.Spec.Template.Spec.ImagePullSecrets.Count);
            Assert.Equal(true, deployment.Spec.Template.Spec.SecurityContext.RunAsNonRoot);
            Assert.Equal(0, deployment.Spec.Template.Spec.SecurityContext.RunAsUser);
        }

        [Fact]
        public void ApplyPodSecurityContextFromCreateOptionsWhenProvided()
        {
            var identity = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "Module1", Mock.Of<ICredentials>());
            var securityContext = new V1PodSecurityContext { RunAsNonRoot = true, RunAsUser = 20001 };
            var config = new KubernetesConfig("image", CreatePodParameters.Create(securityContext: securityContext), Option.Some(new AuthConfig("user-registry1")));
            var module = new KubernetesModule("module1", "v1", "docker", ModuleStatus.Running, RestartPolicy.Always, DefaultConfigurationInfo, EnvVarsDict, config, ImagePullPolicy.OnCreate, EdgeletModuleOwner);
            var labels = new Dictionary<string, string>();
            var mapper = CreateMapper();

            var deployment = mapper.CreateDeployment(identity, module, labels);

            Assert.Equal(1, deployment.Spec.Template.Spec.ImagePullSecrets.Count);
            Assert.Equal(true, deployment.Spec.Template.Spec.SecurityContext.RunAsNonRoot);
            Assert.Equal(20001, deployment.Spec.Template.Spec.SecurityContext.RunAsUser);
        }

        [Fact]
        public void EdgeAgentEnvSettingsHaveLotsOfStuff()
        {
            var identity = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "$edgeAgent", Mock.Of<ICredentials>());
            var config = new KubernetesConfig("image", CreatePodParameters.Create(), Option.Some(new AuthConfig("user-registry1")));
            var module = new KubernetesModule("edgeAgent", "v1", "docker", ModuleStatus.Running, RestartPolicy.Always, DefaultConfigurationInfo, EnvVarsDict, config, ImagePullPolicy.OnCreate, EdgeletModuleOwner);
            var labels = new Dictionary<string, string>();
            var features = new Dictionary<string, bool>
            {
                ["feature1"] = true,
                ["feature2"] = false
            };
            var mapper = CreateMapper(runAsNonRoot: true, persistentVolumeName: "pvname", storageClassName: "scname", proxyImagePullSecretName: "secret name", experimentalFeatures: features);

            var deployment = mapper.CreateDeployment(identity, module, labels);

            var container = deployment.Spec.Template.Spec.Containers.Single(c => c.Name == "edgeagent");
            Assert.Equal(Constants.KubernetesMode, container.Env.Single(e => e.Name == Constants.ModeKey).Value);
            var managementUri = container.Env.Single(e => e.Name == Constants.EdgeletManagementUriVariableName);
            Assert.Equal("http://management/", container.Env.Single(e => e.Name == Constants.EdgeletManagementUriVariableName).Value);
            Assert.Equal("azure-iot-edge", container.Env.Single(e => e.Name == Constants.NetworkIdKey).Value);
            Assert.Equal("proxy", container.Env.Single(e => e.Name == KubernetesConstants.ProxyImageEnvKey).Value);
            Assert.Equal("secret name", container.Env.Single(e => e.Name == KubernetesConstants.ProxyImagePullSecretNameEnvKey).Value);
            Assert.Equal("configPath", container.Env.Single(e => e.Name == KubernetesConstants.ProxyConfigPathEnvKey).Value);
            Assert.Equal("configVolumeName", container.Env.Single(e => e.Name == KubernetesConstants.ProxyConfigVolumeEnvKey).Value);
            Assert.Equal("configMapName", container.Env.Single(e => e.Name == KubernetesConstants.ProxyConfigMapNameEnvKey).Value);
            Assert.Equal("trustBundlePath", container.Env.Single(e => e.Name == KubernetesConstants.ProxyTrustBundlePathEnvKey).Value);
            Assert.Equal("trustBundleVolumeName", container.Env.Single(e => e.Name == KubernetesConstants.ProxyTrustBundleVolumeEnvKey).Value);
            Assert.Equal("trustBundleConfigMapName", container.Env.Single(e => e.Name == KubernetesConstants.ProxyTrustBundleConfigMapEnvKey).Value);
            Assert.Equal("namespace", container.Env.Single(e => e.Name == KubernetesConstants.K8sNamespaceKey).Value);
            Assert.Equal("True", container.Env.Single(e => e.Name == KubernetesConstants.RunAsNonRootKey).Value);
            Assert.Equal("v1", container.Env.Single(e => e.Name == KubernetesConstants.EdgeK8sObjectOwnerApiVersionKey).Value);
            Assert.Equal("Deployment", container.Env.Single(e => e.Name == KubernetesConstants.EdgeK8sObjectOwnerKindKey).Value);
            Assert.Equal("iotedged", container.Env.Single(e => e.Name == KubernetesConstants.EdgeK8sObjectOwnerNameKey).Value);
            Assert.Equal("123", container.Env.Single(e => e.Name == KubernetesConstants.EdgeK8sObjectOwnerUidKey).Value);
            Assert.Equal("ClusterIP", container.Env.Single(e => e.Name == KubernetesConstants.PortMappingServiceType).Value);
            Assert.Equal("False", container.Env.Single(e => e.Name == KubernetesConstants.EnableK8sServiceCallTracingName).Value);
            Assert.Equal("pvname", container.Env.Single(e => e.Name == KubernetesConstants.PersistentVolumeNameKey).Value);
            Assert.Equal("scname", container.Env.Single(e => e.Name == KubernetesConstants.StorageClassNameKey).Value);
            Assert.Equal("100", container.Env.Single(e => e.Name == KubernetesConstants.PersistentVolumeClaimDefaultSizeInMbKey).Value);
            Assert.Equal("True", container.Env.Single(e => e.Name == "feature1").Value);
            Assert.Equal("False", container.Env.Single(e => e.Name == "feature2").Value);
        }

        static KubernetesDeploymentMapper CreateMapper(
          string persistentVolumeName = "",
          string storageClassName = "",
          string proxyImagePullSecretName = null,
          bool runAsNonRoot = false,
          IDictionary<string, bool> experimentalFeatures = null)
            => new KubernetesDeploymentMapper(
                "namespace",
                "edgehub",
                "proxy",
                Option.Maybe(proxyImagePullSecretName),
                "configPath",
                "configVolumeName",
                "configMapName",
                "trustBundlePath",
                "trustBundleVolumeName",
                "trustBundleConfigMapName",
                PortMapServiceType.ClusterIP,
                persistentVolumeName,
                storageClassName,
                Option.Some<uint>(100),
                "apiVersion",
                new Uri("http://workload"),
                new Uri("http://management"),
                runAsNonRoot,
                false,
                experimentalFeatures == null ? new Dictionary<string, bool>() : experimentalFeatures);
    }
}
