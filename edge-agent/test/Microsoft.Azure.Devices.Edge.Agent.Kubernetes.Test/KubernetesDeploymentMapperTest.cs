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

        static readonly HostConfig NoTypeVolumeMountHostConfig = new HostConfig
        {
            Mounts = new List<Mount>
            {
                new Mount
                {
                    ReadOnly = true,
                    Source = "a-volume",
                    Target = "/tmp/volume"
                }
            }
        };

        static readonly HostConfig NoSourceVolumeMountHostConfig = new HostConfig
        {
            Mounts = new List<Mount>
            {
                new Mount
                {
                    Type = "volume",
                    ReadOnly = true,
                    Target = "/tmp/volume"
                }
            }
        };

        static readonly HostConfig NoTargetVolumeMountHostConfig = new HostConfig
        {
            Mounts = new List<Mount>
            {
                new Mount
                {
                    Type = "volume",
                    ReadOnly = true,
                    Source = "a-volume",
                }
            }
        };

        static readonly HostConfig DuplicateVolumesHostConfig = new HostConfig
        {
            Binds = new List<string>
            {
                "/home/bind:/home/bind1",
                "/home/bind:/home/bind2:ro"
            },
            Mounts = new List<Mount>
            {
                new Mount
                {
                    Type = "volume",
                    ReadOnly = true,
                    Source = "a-volume",
                    Target = "/tmp/volume1"
                },
                new Mount
                {
                    Type = "volume",
                    ReadOnly = false,
                    Source = "a-volume",
                    Target = "/tmp/volume2"
                },
                new Mount
                {
                    Type = "volume",
                    ReadOnly = true,
                    Source = "b-volume",
                    Target = "/tmp/volume3"
                },
                new Mount
                {
                    Type = "volume",
                    ReadOnly = true,
                    Source = "b-volume",
                    Target = "/tmp/volume4"
                },
                new Mount
                {
                    Type = "Bind",
                    ReadOnly = true,
                    Source = "/home/data/test",
                    Target = "/home/test1"
                },
                new Mount
                {
                    Type = "Bind",
                    ReadOnly = false,
                    Source = "/home/data/test",
                    Target = "/home/test2"
                },
            }
        };

        static readonly HostConfig HostIpcModeHostConfig = new HostConfig
        {
            IpcMode = "host"
        };

        static readonly HostConfig PrivateIpcModeHostConfig = new HostConfig
        {
            IpcMode = "private"
        };

        static readonly HostConfig HostPidModeHostConfig = new HostConfig
        {
            PidMode = "host"
        };

        static readonly HostConfig ContainerPidModeHostConfig = new HostConfig
        {
            PidMode = "container:module-a"
        };

        static readonly HostConfig HostNetworkModeHostConfig = new HostConfig
        {
            NetworkMode = "host"
        };

        static readonly HostConfig BridgeNetworkModeHostConfig = new HostConfig
        {
            NetworkMode = "bridge"
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
            var docker = new DockerModule("module1", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, Constants.DefaultStartupOrder, DefaultConfigurationInfo, EnvVarsDict);
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
            var docker = new DockerModule("module1", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, Constants.DefaultStartupOrder, DefaultConfigurationInfo, EnvVarsDict);
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
            var docker = new DockerModule("module1", "v1", ModuleStatus.Stopped, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, Constants.DefaultStartupOrder, DefaultConfigurationInfo, EnvVarsDict);
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
            var docker = new DockerModule("module1", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, Constants.DefaultStartupOrder, DefaultConfigurationInfo, EnvVarsDict);
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
        public void PvcMappingByDefault()
        {
            var identity = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "ModuleId", Mock.Of<ICredentials>());
            var labels = new Dictionary<string, string>();
            var hostConfig = VolumeMountHostConfig;
            var config = new KubernetesConfig("image", CreatePodParameters.Create(labels: labels, hostConfig: hostConfig), Option.None<AuthConfig>());
            var docker = new DockerModule("module1", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, Constants.DefaultStartupOrder, DefaultConfigurationInfo, EnvVarsDict);
            var module = new KubernetesModule(docker, config, EdgeletModuleOwner);
            var mapper = CreateMapper(storageClassName: null);
            var deployment = mapper.CreateDeployment(identity, module, labels);
            var pod = deployment.Spec.Template;

            Assert.True(pod != null);
            var podVolume = pod.Spec.Volumes.Single(v => v.Name == "a-volume");
            Assert.NotNull(podVolume.PersistentVolumeClaim);
            Assert.Equal("a-volume", podVolume.PersistentVolumeClaim.ClaimName);
            Assert.True(podVolume.PersistentVolumeClaim.ReadOnlyProperty);
            var podVolumeMount = pod.Spec.Containers.Single(p => p.Name != "proxy").VolumeMounts.Single(vm => vm.Name == "a-volume");
            Assert.Equal("/tmp/volume", podVolumeMount.MountPath);
            Assert.True(podVolumeMount.ReadOnlyProperty);
        }

        [Fact]
        public void PvcMappingForVolumeNameVolume()
        {
            var identity = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "ModuleId", Mock.Of<ICredentials>());
            var labels = new Dictionary<string, string>();
            var hostConfig = VolumeMountHostConfig;
            var config = new KubernetesConfig("image", CreatePodParameters.Create(labels: labels, hostConfig: hostConfig), Option.None<AuthConfig>());
            var docker = new DockerModule("module1", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, Constants.DefaultStartupOrder, DefaultConfigurationInfo, EnvVarsDict);
            var module = new KubernetesModule(docker, config, EdgeletModuleOwner);
            var mapper = CreateMapper(true, null);

            var deployment = mapper.CreateDeployment(identity, module, labels);

            var pod = deployment.Spec.Template;

            Assert.True(pod != null);
            var podVolume = pod.Spec.Volumes.Single(v => v.Name == "a-volume");
            Assert.NotNull(podVolume.PersistentVolumeClaim);
            Assert.Equal("a-volume", podVolume.PersistentVolumeClaim.ClaimName);
            Assert.True(podVolume.PersistentVolumeClaim.ReadOnlyProperty);
            var podVolumeMount = pod.Spec.Containers.Single(p => p.Name != "proxy").VolumeMounts.Single(vm => vm.Name == "a-volume");
            Assert.Equal("/tmp/volume", podVolumeMount.MountPath);
            Assert.True(podVolumeMount.ReadOnlyProperty);
        }

        [Fact]
        public void VolumeMountEliminateDuplicates()
        {
            var identity = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "ModuleId", Mock.Of<ICredentials>());
            var labels = new Dictionary<string, string>();
            var hostConfig = DuplicateVolumesHostConfig;
            var config = new KubernetesConfig("image", CreatePodParameters.Create(labels: labels, hostConfig: hostConfig), Option.None<AuthConfig>());
            var docker = new DockerModule("module1", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, Constants.DefaultStartupOrder, DefaultConfigurationInfo, EnvVarsDict);
            var module = new KubernetesModule(docker, config, EdgeletModuleOwner);
            var mapper = CreateMapper(true, null);

            var deployment = mapper.CreateDeployment(identity, module, labels);

            var pod = deployment.Spec.Template;

            Assert.True(pod != null);
            // There are 2 Mounts from same Volume source,
            // translates to 1 Volume and 2 VolumeMounts
            var volASourceVolume = pod.Spec.Volumes.Single(v => v.Name == "a-volume");
            Assert.NotNull(volASourceVolume.PersistentVolumeClaim);
            Assert.Equal("a-volume", volASourceVolume.PersistentVolumeClaim.ClaimName);
            Assert.False(volASourceVolume.PersistentVolumeClaim.ReadOnlyProperty);
            var volASourceVolumeMounts = pod.Spec.Containers.Single(p => p.Name != "proxy").VolumeMounts.Where(vm => vm.Name == "a-volume");
            var volASourceVM1 = volASourceVolumeMounts.Single(vm => vm.MountPath == "/tmp/volume1");
            var volASourceVM2 = volASourceVolumeMounts.Single(vm => vm.MountPath == "/tmp/volume2");
            Assert.True(volASourceVM1.ReadOnlyProperty);
            Assert.False(volASourceVM2.ReadOnlyProperty);
            // There are 2 Mounts from same Volume source,
            // translates to 1 Volume and 2 VolumeMounts
            var volBSourceVolume = pod.Spec.Volumes.Single(v => v.Name == "b-volume");
            Assert.NotNull(volBSourceVolume.PersistentVolumeClaim);
            Assert.Equal("b-volume", volBSourceVolume.PersistentVolumeClaim.ClaimName);
            Assert.True(volBSourceVolume.PersistentVolumeClaim.ReadOnlyProperty);
            var volBSourceVolumeMounts = pod.Spec.Containers.Single(p => p.Name != "proxy").VolumeMounts.Where(vm => vm.Name == "b-volume");
            var volBSourceVM1 = volBSourceVolumeMounts.Single(vm => vm.MountPath == "/tmp/volume3");
            var volBSourceVM2 = volBSourceVolumeMounts.Single(vm => vm.MountPath == "/tmp/volume4");
            Assert.True(volBSourceVM1.ReadOnlyProperty);
            Assert.True(volBSourceVM2.ReadOnlyProperty);
            // There are 2 Bind Mounts from same Bind Mount source,
            // translates to 1 Volume and 2 VolumeMounts
            var bindMountVolume = pod.Spec.Volumes.Single(v => v.Name == "homedatatest");
            Assert.NotNull(bindMountVolume.HostPath);
            Assert.Equal("/home/data/test", bindMountVolume.HostPath.Path);
            var bindMountVolumeMounts = pod.Spec.Containers.Single(p => p.Name != "proxy").VolumeMounts.Where(vm => vm.Name == "homedatatest");
            var bindMountVM1 = bindMountVolumeMounts.Single(vm => vm.MountPath == "/home/test1");
            var bindMountVM2 = bindMountVolumeMounts.Single(vm => vm.MountPath == "/home/test2");
            Assert.True(bindMountVM1.ReadOnlyProperty);
            Assert.False(bindMountVM2.ReadOnlyProperty);
            // There are 2 Bind from same dir source,
            // translates to 1 Volume and 2 VolumeMounts
            var bindVolume = pod.Spec.Volumes.Single(v => v.Name == "homebind");
            Assert.NotNull(bindVolume.HostPath);
            Assert.Equal("/home/bind", bindVolume.HostPath.Path);
            var bindVolumeMounts = pod.Spec.Containers.Single(p => p.Name != "proxy").VolumeMounts.Where(vm => vm.Name == "homebind");
            var bindVM1 = bindVolumeMounts.Single(vm => vm.MountPath == "/home/bind1");
            var bindVM2 = bindVolumeMounts.Single(vm => vm.MountPath == "/home/bind2");
            Assert.False(bindVM1.ReadOnlyProperty);
            Assert.True(bindVM2.ReadOnlyProperty);
        }

        [Fact]
        public void PvcMappingForStorageClassVolume()
        {
            var identity = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "ModuleId", Mock.Of<ICredentials>());
            var labels = new Dictionary<string, string>();
            var hostConfig = VolumeMountHostConfig;
            var config = new KubernetesConfig("image", CreatePodParameters.Create(labels: labels, hostConfig: hostConfig), Option.None<AuthConfig>());
            var docker = new DockerModule("module1", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, Constants.DefaultStartupOrder, DefaultConfigurationInfo, EnvVarsDict);
            var module = new KubernetesModule(docker, config, EdgeletModuleOwner);
            var mapper = CreateMapper();

            var deployment = mapper.CreateDeployment(identity, module, labels);
            var pod = deployment.Spec.Template;

            Assert.True(pod != null);
            var podVolume = pod.Spec.Volumes.Single(v => v.Name == "a-volume");
            Assert.NotNull(podVolume.PersistentVolumeClaim);
            Assert.Equal("a-volume", podVolume.PersistentVolumeClaim.ClaimName);
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
            var docker = new DockerModule("module1", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, Constants.DefaultStartupOrder, DefaultConfigurationInfo, EnvVarsDict);
            var module = new KubernetesModule(docker, config, EdgeletModuleOwner);
            var mapper = CreateMapper();

            var deployment = mapper.CreateDeployment(identity, module, labels);
            var pod = deployment.Spec.Template;

            Assert.True(pod != null);
            var podVolume = pod.Spec.Volumes.Single(v => v.Name == "a-volume");
            Assert.NotNull(podVolume.PersistentVolumeClaim);
            Assert.Equal("a-volume", podVolume.PersistentVolumeClaim.ClaimName);
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
            var docker = new DockerModule("module1", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, Constants.DefaultStartupOrder, DefaultConfigurationInfo, EnvVarsDict);
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
            var docker = new DockerModule("module1", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, Constants.DefaultStartupOrder, DefaultConfigurationInfo, EnvVarsDict);
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
            var docker = new DockerModule("module1", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, Constants.DefaultStartupOrder, DefaultConfigurationInfo, EnvVarsDict);
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
            var docker = new DockerModule("module1", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, Constants.DefaultStartupOrder, DefaultConfigurationInfo, EnvVarsDict);
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
        public void AppliesDefaultResourcesForAgent()
        {
            var identity = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "$edgeAgent", Mock.Of<ICredentials>());
            var docker = new DockerModule("edgeAgent", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, Core.Constants.DefaultStartupOrder, DefaultConfigurationInfo, EnvVarsDict);
            var agentResources = new V1ResourceRequirements(
                new Dictionary<string, ResourceQuantity>
                {
                    ["memory"] = new ResourceQuantity("1500Mi"),
                    ["cpu"] = new ResourceQuantity("150m"),
                },
                new Dictionary<string, ResourceQuantity>
                {
                    ["memory"] = new ResourceQuantity("1500Mi"),
                    ["cpu"] = new ResourceQuantity("150m"),
                });
            var config = new KubernetesConfig("image", CreatePodParameters.Create(), Option.None<AuthConfig>());
            var module = new KubernetesModule(docker, config, EdgeletModuleOwner);
            var labels = new Dictionary<string, string>();
            var mapper = CreateMapper();

            var deployment = mapper.CreateDeployment(identity, module, labels);

            var moduleContainer = deployment.Spec.Template.Spec.Containers.Single(container => container.Name == "edgeagent");
            Assert.Equal(agentResources.Limits, moduleContainer.Resources.Limits);
            Assert.Equal(agentResources.Requests, moduleContainer.Resources.Requests);
        }

        [Fact]
        public void AppliesDefaultResourcesForProxy()
        {
            var identity = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "Module1", Mock.Of<ICredentials>());
            var docker = new DockerModule("module1", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, Core.Constants.DefaultStartupOrder, DefaultConfigurationInfo, EnvVarsDict);
            var proxyResources = new V1ResourceRequirements(
                new Dictionary<string, ResourceQuantity>
                {
                    ["memory"] = new ResourceQuantity("1000M"),
                    ["cpu"] = new ResourceQuantity("20m"),
                },
                new Dictionary<string, ResourceQuantity>
                {
                    ["memory"] = new ResourceQuantity("1000M"),
                    ["cpu"] = new ResourceQuantity("20m"),
                });
            var config = new KubernetesConfig("image", CreatePodParameters.Create(), Option.None<AuthConfig>());
            var module = new KubernetesModule(docker, config, EdgeletModuleOwner);
            var labels = new Dictionary<string, string>();
            var mapper = CreateMapper();

            var deployment = mapper.CreateDeployment(identity, module, labels);

            var moduleContainer = deployment.Spec.Template.Spec.Containers.Single(container => container.Name == "proxy");
            Assert.Equal(proxyResources.Limits, moduleContainer.Resources.Limits);
            Assert.Equal(proxyResources.Requests, moduleContainer.Resources.Requests);
        }

        [Fact]
        public void LeaveResourcesEmptyWhenNothingProvidedInCreateOptions()
        {
            var identity = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "Module1", Mock.Of<ICredentials>());
            var docker = new DockerModule("module1", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, Constants.DefaultStartupOrder, DefaultConfigurationInfo, EnvVarsDict);
            var config = new KubernetesConfig("image", CreatePodParameters.Create(), Option.None<AuthConfig>());
            var module = new KubernetesModule(docker, config, EdgeletModuleOwner);
            var labels = new Dictionary<string, string>();
            var mapper = CreateMapper();

            var deployment = mapper.CreateDeployment(identity, module, labels);

            var moduleContainer = deployment.Spec.Template.Spec.Containers.Single(container => container.Name == "module1");
            Assert.Null(moduleContainer.Resources);
        }

        [Fact]
        public void AppliesAgentConfigMapVolumeToContainerSpec()
        {
            var identity = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "$edgeAgent", Mock.Of<ICredentials>());
            var docker = new DockerModule("edgeagent", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, Constants.DefaultStartupOrder, DefaultConfigurationInfo, EnvVarsDict);
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
            var agentContainer = deployment.Spec.Template.Spec.Containers.Single(container => container.Name == "edgeagent");
            Assert.Equal(2, agentContainer.VolumeMounts.Count);
            Assert.Contains(agentContainer.VolumeMounts, vm => vm.Name.Equals("agentConfigVolume"));
            Assert.Contains(agentContainer.VolumeMounts, vm => vm.Name.Equals("additional-volume"));
        }

        [Fact]
        public void DoesNotApplyAgentConfigMapVolumeToContainerSpecWhenNoSettings()
        {
            var identity = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "$edgeAgent", Mock.Of<ICredentials>());
            var docker = new DockerModule("edgeagent", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, Core.Constants.DefaultStartupOrder, DefaultConfigurationInfo, EnvVarsDict);
            var volumes = new[]
            {
                new KubernetesModuleVolumeSpec(
                    new V1Volume("additional-volume", configMap: new V1ConfigMapVolumeSource(name: "additional-config-map")),
                    new[] { new V1VolumeMount(name: "additional-volume", mountPath: "/etc") })
            };
            var config = new KubernetesConfig("image", CreatePodParameters.Create(volumes: volumes), Option.None<AuthConfig>());
            var module = new KubernetesModule(docker, config, EdgeletModuleOwner);
            var labels = new Dictionary<string, string>();
            var mapper1 = CreateMapper(agentConfigMapName: null);
            var mapper2 = CreateMapper(agentConfigPath: null);
            var mapper3 = CreateMapper(agentConfigVolume: null);

            var deployment1 = mapper1.CreateDeployment(identity, module, labels);
            var deployment2 = mapper2.CreateDeployment(identity, module, labels);
            var deployment3 = mapper3.CreateDeployment(identity, module, labels);

            // Validate module volume mounts
            var agentContainer1 = deployment1.Spec.Template.Spec.Containers.Single(container => container.Name == "edgeagent");
            Assert.Equal(1, agentContainer1.VolumeMounts.Count);
            Assert.Contains(agentContainer1.VolumeMounts, vm => vm.Name.Equals("additional-volume"));
            var agentContainer2 = deployment2.Spec.Template.Spec.Containers.Single(container => container.Name == "edgeagent");
            Assert.Equal(1, agentContainer2.VolumeMounts.Count);
            Assert.Contains(agentContainer2.VolumeMounts, vm => vm.Name.Equals("additional-volume"));
            var agentContainer3 = deployment3.Spec.Template.Spec.Containers.Single(container => container.Name == "edgeagent");
            Assert.Equal(1, agentContainer3.VolumeMounts.Count);
            Assert.Contains(agentContainer3.VolumeMounts, vm => vm.Name.Equals("additional-volume"));
        }

        [Fact]
        public void AppliesVolumesFromCreateOptionsToContainerSpec()
        {
            var identity = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "Module1", Mock.Of<ICredentials>());
            var docker = new DockerModule("module1", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, Constants.DefaultStartupOrder, DefaultConfigurationInfo, EnvVarsDict);
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
            var docker = new DockerModule("module1", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, Constants.DefaultStartupOrder, DefaultConfigurationInfo, EnvVarsDict);
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
            var docker = new DockerModule("module1", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, Constants.DefaultStartupOrder, DefaultConfigurationInfo, EnvVarsDict);
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
            var module = new KubernetesModule("module1", "v1", "docker", ModuleStatus.Running, Core.RestartPolicy.Always, DefaultConfigurationInfo, EnvVarsDict, config, ImagePullPolicy.OnCreate, EdgeletModuleOwner);
            var labels = new Dictionary<string, string>();
            var mapper = CreateMapper(proxyImagePullSecretName: "user-registry2");

            var deployment = mapper.CreateDeployment(identity, module, labels);

            Assert.Equal(2, deployment.Spec.Template.Spec.ImagePullSecrets.Count);
            Assert.Contains(deployment.Spec.Template.Spec.ImagePullSecrets, secret => secret.Name == "user-registry1");
            Assert.Contains(deployment.Spec.Template.Spec.ImagePullSecrets, secret => secret.Name == "user-registry2");
        }

        [Fact]
        public void FailMapWhenMountWhenMountTypeIsMissing()
        {
            var identity = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "Module1", Mock.Of<ICredentials>());
            var hostConfig = NoTypeVolumeMountHostConfig;
            var config = new KubernetesConfig("image", CreatePodParameters.Create(hostConfig: hostConfig), Option.Some(new AuthConfig("user-registry1")));
            var module = new KubernetesModule("module1", "v1", "docker", ModuleStatus.Running, Core.RestartPolicy.Always, DefaultConfigurationInfo, EnvVarsDict, config, ImagePullPolicy.OnCreate, EdgeletModuleOwner);
            var labels = new Dictionary<string, string>();
            var mapper = CreateMapper(proxyImagePullSecretName: "user-registry2");

            Assert.Throws<InvalidMountException>(() => mapper.CreateDeployment(identity, module, labels));
        }

        [Fact]
        public void FailMapWhenMountWhenMountSourceIsMissing()
        {
            var identity = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "Module1", Mock.Of<ICredentials>());
            var hostConfig = NoSourceVolumeMountHostConfig;
            var config = new KubernetesConfig("image", CreatePodParameters.Create(hostConfig: hostConfig), Option.Some(new AuthConfig("user-registry1")));
            var module = new KubernetesModule("module1", "v1", "docker", ModuleStatus.Running, Core.RestartPolicy.Always, DefaultConfigurationInfo, EnvVarsDict, config, ImagePullPolicy.OnCreate, EdgeletModuleOwner);
            var labels = new Dictionary<string, string>();
            var mapper = CreateMapper(proxyImagePullSecretName: "user-registry2");

            Assert.Throws<InvalidMountException>(() => mapper.CreateDeployment(identity, module, labels));
        }

        [Fact]
        public void FailMapWhenMountWhenMountTargetIsMissing()
        {
            var identity = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "Module1", Mock.Of<ICredentials>());
            var hostConfig = NoTargetVolumeMountHostConfig;
            var config = new KubernetesConfig("image", CreatePodParameters.Create(hostConfig: hostConfig), Option.Some(new AuthConfig("user-registry1")));
            var module = new KubernetesModule("module1", "v1", "docker", ModuleStatus.Running, Core.RestartPolicy.Always, DefaultConfigurationInfo, EnvVarsDict, config, ImagePullPolicy.OnCreate, EdgeletModuleOwner);
            var labels = new Dictionary<string, string>();
            var mapper = CreateMapper(proxyImagePullSecretName: "user-registry2");

            Assert.Throws<InvalidMountException>(() => mapper.CreateDeployment(identity, module, labels));
        }

        [Fact]
        public void PassOnlyOneImagePullSecretInPodSpecIfProxyAndModuleContainersHasTheSameImagePullSecrets()
        {
            var identity = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "Module1", Mock.Of<ICredentials>());
            var config = new KubernetesConfig("image", CreatePodParameters.Create(), Option.Some(new AuthConfig("user-registry1")));
            var module = new KubernetesModule("module1", "v1", "docker", ModuleStatus.Running, Core.RestartPolicy.Always, DefaultConfigurationInfo, EnvVarsDict, config, ImagePullPolicy.OnCreate, EdgeletModuleOwner);
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
            var module = new KubernetesModule("module1", "v1", "docker", ModuleStatus.Running, Core.RestartPolicy.Always, DefaultConfigurationInfo, EnvVarsDict, config, ImagePullPolicy.OnCreate, EdgeletModuleOwner);
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
            var module = new KubernetesModule("module1", "v1", "docker", ModuleStatus.Running, Core.RestartPolicy.Always, DefaultConfigurationInfo, EnvVarsDict, config, ImagePullPolicy.OnCreate, EdgeletModuleOwner);
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
            var module = new KubernetesModule("module1", "v1", "docker", ModuleStatus.Running, Core.RestartPolicy.Always, DefaultConfigurationInfo, EnvVarsDict, config, ImagePullPolicy.OnCreate, EdgeletModuleOwner);
            var labels = new Dictionary<string, string>();
            var mapper = CreateMapper();

            var deployment = mapper.CreateDeployment(identity, module, labels);

            Assert.Equal(1, deployment.Spec.Template.Spec.ImagePullSecrets.Count);
            Assert.Equal(true, deployment.Spec.Template.Spec.SecurityContext.RunAsNonRoot);
            Assert.Equal(20001, deployment.Spec.Template.Spec.SecurityContext.RunAsUser);
        }

        [Fact]
        public void LeavesStrategyEmptyWhenNotProvided()
        {
            var identity = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "Module1", Mock.Of<ICredentials>());
            var config = new KubernetesConfig("image", CreatePodParameters.Create(), Option.Some(new AuthConfig("user-registry1")));
            var module = new KubernetesModule("module1", "v1", "docker", ModuleStatus.Running, Core.RestartPolicy.Always, DefaultConfigurationInfo, EnvVarsDict, config, ImagePullPolicy.OnCreate, EdgeletModuleOwner);
            var labels = new Dictionary<string, string>();
            var mapper = CreateMapper();

            var deployment = mapper.CreateDeployment(identity, module, labels);

            Assert.Null(deployment.Spec.Strategy);
        }

        [Fact]
        public void ApplyDeploymentStrategyWhenProvided()
        {
            var identity = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "Module1", Mock.Of<ICredentials>());
            var deploymentStrategy = new V1DeploymentStrategy { Type = "Recreate" };
            var config = new KubernetesConfig("image", CreatePodParameters.Create(deploymentStrategy: deploymentStrategy), Option.Some(new AuthConfig("user-registry1")));
            var module = new KubernetesModule("module1", "v1", "docker", ModuleStatus.Running, Core.RestartPolicy.Always, DefaultConfigurationInfo, EnvVarsDict, config, ImagePullPolicy.OnCreate, EdgeletModuleOwner);
            var labels = new Dictionary<string, string>();
            var mapper = CreateMapper();

            var deployment = mapper.CreateDeployment(identity, module, labels);

            Assert.Equal("Recreate", deployment.Spec.Strategy.Type);
        }

        [Fact]
        public void NoCmdOptionsNoContainerArgs()
        {
            var identity = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "Module1", Mock.Of<ICredentials>());
            var config = new KubernetesConfig("image", CreatePodParameters.Create(), Option.Some(new AuthConfig("user-registry1")));
            var module = new KubernetesModule("module1", "v1", "docker", ModuleStatus.Running, Core.RestartPolicy.Always, DefaultConfigurationInfo, EnvVarsDict, config, ImagePullPolicy.OnCreate, EdgeletModuleOwner);
            var labels = new Dictionary<string, string>();
            var mapper = CreateMapper();

            var deployment = mapper.CreateDeployment(identity, module, labels);

            var container = deployment.Spec.Template.Spec.Containers.Single(c => c.Name == "module1");
            Assert.Null(container.Args);
            Assert.Null(container.Command);
            Assert.Null(container.WorkingDir);
        }

        [Fact]
        public void CmdOptionsContainerArgs()
        {
            var cmd = new List<string> { "argument1", "argument2" };
            var identity = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "Module1", Mock.Of<ICredentials>());
            var config = new KubernetesConfig("image", CreatePodParameters.Create(cmd: cmd), Option.Some(new AuthConfig("user-registry1")));
            var module = new KubernetesModule("module1", "v1", "docker", ModuleStatus.Running, Core.RestartPolicy.Always, DefaultConfigurationInfo, EnvVarsDict, config, ImagePullPolicy.OnCreate, EdgeletModuleOwner);
            var labels = new Dictionary<string, string>();
            var mapper = CreateMapper();

            var deployment = mapper.CreateDeployment(identity, module, labels);

            var container = deployment.Spec.Template.Spec.Containers.Single(c => c.Name == "module1");
            Assert.NotNull(container.Args);
            Assert.Equal(2, container.Args.Count);
            Assert.Equal("argument1", container.Args[0]);
            Assert.Equal("argument2", container.Args[1]);
        }

        [Fact]
        public void EntrypointOptionsContainerCommands()
        {
            var entrypoint = new List<string> { "command", "argument-a" };
            var identity = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "Module1", Mock.Of<ICredentials>());
            var config = new KubernetesConfig("image", CreatePodParameters.Create(entrypoint: entrypoint), Option.Some(new AuthConfig("user-registry1")));
            var module = new KubernetesModule("module1", "v1", "docker", ModuleStatus.Running, Core.RestartPolicy.Always, DefaultConfigurationInfo, EnvVarsDict, config, ImagePullPolicy.OnCreate, EdgeletModuleOwner);
            var labels = new Dictionary<string, string>();
            var mapper = CreateMapper();

            var deployment = mapper.CreateDeployment(identity, module, labels);

            var container = deployment.Spec.Template.Spec.Containers.Single(c => c.Name == "module1");
            Assert.NotNull(container.Command);
            Assert.Equal(2, container.Command.Count);
            Assert.Equal("command", container.Command[0]);
            Assert.Equal("argument-a", container.Command[1]);
        }

        [Fact]
        public void WorkingDirOptionsContainerWorkingDir()
        {
            var identity = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "Module1", Mock.Of<ICredentials>());
            var config = new KubernetesConfig("image", CreatePodParameters.Create(workingDir: "/tmp/working"), Option.Some(new AuthConfig("user-registry1")));
            var module = new KubernetesModule("module1", "v1", "docker", ModuleStatus.Running, Core.RestartPolicy.Always, DefaultConfigurationInfo, EnvVarsDict, config, ImagePullPolicy.OnCreate, EdgeletModuleOwner);
            var labels = new Dictionary<string, string>();
            var mapper = CreateMapper();

            var deployment = mapper.CreateDeployment(identity, module, labels);

            var container = deployment.Spec.Template.Spec.Containers.Single(c => c.Name == "module1");
            Assert.NotNull(container.WorkingDir);
            Assert.Equal("/tmp/working", container.WorkingDir);
        }

        [Fact]
        public void EnvModuleSettingsParseCorrectly()
        {
            var env = new List<string>
            {
                "a=b",
                "ALL_EQUALS=====",
                "HAS_SPACES=this variable has spaces",
                "B=b=c",
                "BASE64_TEXT=YmFzZTY0Cg==",
                "==not a valid env var",
            };
            var identity = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "Module1", Mock.Of<ICredentials>());
            var config = new KubernetesConfig("image", CreatePodParameters.Create(env: env), Option.Some(new AuthConfig("user-registry1")));
            var module = new KubernetesModule("module1", "v1", "docker", ModuleStatus.Running, Core.RestartPolicy.Always, DefaultConfigurationInfo, EnvVarsDict, config, ImagePullPolicy.OnCreate, EdgeletModuleOwner);
            var labels = new Dictionary<string, string>();
            var features = new Dictionary<string, bool>
            {
                ["feature1"] = true,
                ["feature2"] = false
            };
            var mapper = CreateMapper(runAsNonRoot: true, useMountSourceForVolumeName: true, storageClassName: "scname", proxyImagePullSecretName: "secret name", experimentalFeatures: features);

            var deployment = mapper.CreateDeployment(identity, module, labels);

            var container = deployment.Spec.Template.Spec.Containers.Single(c => c.Name == "module1");
            Assert.Equal("b", container.Env.Single(e => e.Name == "a").Value);
            Assert.Equal("====", container.Env.Single(e => e.Name == "ALL_EQUALS").Value);
            Assert.Equal("this variable has spaces", container.Env.Single(e => e.Name == "HAS_SPACES").Value);
            Assert.Equal("b=c", container.Env.Single(e => e.Name == "B").Value);
            Assert.Equal("YmFzZTY0Cg==", container.Env.Single(e => e.Name == "BASE64_TEXT").Value);
            Assert.Null(container.Env.SingleOrDefault(e => e.Value.EndsWith("valid env var")));
        }

        [Fact]
        public void EdgeAgentEnvSettingsHaveLotsOfStuff()
        {
            var identity = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "$edgeAgent", Mock.Of<ICredentials>());
            var config = new KubernetesConfig("image", CreatePodParameters.Create(), Option.Some(new AuthConfig("user-registry1")));
            var module = new KubernetesModule("edgeAgent", "v1", "docker", ModuleStatus.Running, Core.RestartPolicy.Always, DefaultConfigurationInfo, EnvVarsDict, config, ImagePullPolicy.OnCreate, EdgeletModuleOwner);
            var labels = new Dictionary<string, string>();
            var features = new Dictionary<string, bool>
            {
                ["feature1"] = true,
                ["feature2"] = false
            };
            var mapper = CreateMapper(runAsNonRoot: true, useMountSourceForVolumeName: true, storageClassName: "scname", proxyImagePullSecretName: "secret name", experimentalFeatures: features);

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
            Assert.Equal("True", container.Env.Single(e => e.Name == KubernetesConstants.UseMountSourceForVolumeNameKey).Value);
            Assert.Equal("scname", container.Env.Single(e => e.Name == KubernetesConstants.StorageClassNameKey).Value);
            Assert.Equal("100", container.Env.Single(e => e.Name == KubernetesConstants.PersistentVolumeClaimDefaultSizeInMbKey).Value);
            Assert.Equal("True", container.Env.Single(e => e.Name == "feature1").Value);
            Assert.Equal("False", container.Env.Single(e => e.Name == "feature2").Value);
        }

        [Fact]
        public void NoIpcNetworkPidDeploymentCreation()
        {
            var identity = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "Module1", Mock.Of<ICredentials>());
            var config = new KubernetesConfig("image", CreatePodParameters.Create(), Option.None<AuthConfig>());
            var docker = new DockerModule("module1", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, Core.Constants.DefaultStartupOrder, DefaultConfigurationInfo, EnvVarsDict);
            var module = new KubernetesModule(docker, config, EdgeletModuleOwner);
            var labels = new Dictionary<string, string>();
            var mapper = CreateMapper();

            var deployment = mapper.CreateDeployment(identity, module, labels);

            Assert.NotNull(deployment);
            Assert.Null(deployment.Spec.Template.Spec.HostIPC);
            Assert.Null(deployment.Spec.Template.Spec.HostNetwork);
            Assert.Null(deployment.Spec.Template.Spec.DnsPolicy);
            Assert.Null(deployment.Spec.Template.Spec.HostPID);
        }

        [Fact]
        public void HostIpcModeDeploymentCreation()
        {
            var identity = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "Module1", Mock.Of<ICredentials>());
            var config = new KubernetesConfig("image", CreatePodParameters.Create(hostConfig: HostIpcModeHostConfig), Option.None<AuthConfig>());
            var docker = new DockerModule("module1", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, Core.Constants.DefaultStartupOrder, DefaultConfigurationInfo, EnvVarsDict);
            var module = new KubernetesModule(docker, config, EdgeletModuleOwner);
            var labels = new Dictionary<string, string>();
            var mapper = CreateMapper();

            var deployment = mapper.CreateDeployment(identity, module, labels);

            Assert.NotNull(deployment);
            Assert.Equal(true, deployment.Spec.Template.Spec.HostIPC);
        }

        [Fact]
        public void PrivateIpcModeDeploymentCreation()
        {
            var identity = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "Module1", Mock.Of<ICredentials>());
            var config = new KubernetesConfig("image", CreatePodParameters.Create(hostConfig: PrivateIpcModeHostConfig), Option.None<AuthConfig>());
            var docker = new DockerModule("module1", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, Core.Constants.DefaultStartupOrder, DefaultConfigurationInfo, EnvVarsDict);
            var module = new KubernetesModule(docker, config, EdgeletModuleOwner);
            var labels = new Dictionary<string, string>();
            var mapper = CreateMapper();

            var deployment = mapper.CreateDeployment(identity, module, labels);

            Assert.NotNull(deployment);
            Assert.Equal(false, deployment.Spec.Template.Spec.HostIPC);
        }

        [Fact]
        public void HostNetworkModeDeploymentCreation()
        {
            var identity = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "Module1", Mock.Of<ICredentials>());
            var config = new KubernetesConfig("image", CreatePodParameters.Create(hostConfig: HostNetworkModeHostConfig), Option.None<AuthConfig>());
            var docker = new DockerModule("module1", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, Core.Constants.DefaultStartupOrder, DefaultConfigurationInfo, EnvVarsDict);
            var module = new KubernetesModule(docker, config, EdgeletModuleOwner);
            var labels = new Dictionary<string, string>();
            var mapper = CreateMapper();

            var deployment = mapper.CreateDeployment(identity, module, labels);

            Assert.NotNull(deployment);
            Assert.Equal(true, deployment.Spec.Template.Spec.HostNetwork);
            Assert.Equal("ClusterFirstWithHostNet", deployment.Spec.Template.Spec.DnsPolicy);
        }

        [Fact]
        public void BridgeNetworkModeDeploymentCreation()
        {
            var identity = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "Module1", Mock.Of<ICredentials>());
            var config = new KubernetesConfig("image", CreatePodParameters.Create(hostConfig: BridgeNetworkModeHostConfig), Option.None<AuthConfig>());
            var docker = new DockerModule("module1", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, Core.Constants.DefaultStartupOrder, DefaultConfigurationInfo, EnvVarsDict);
            var module = new KubernetesModule(docker, config, EdgeletModuleOwner);
            var labels = new Dictionary<string, string>();
            var mapper = CreateMapper();

            var deployment = mapper.CreateDeployment(identity, module, labels);

            Assert.NotNull(deployment);
            Assert.Equal(false, deployment.Spec.Template.Spec.HostNetwork);
            Assert.Null(deployment.Spec.Template.Spec.DnsPolicy);
        }

        [Fact]
        public void HostPidModeDeploymentCreation()
        {
            var identity = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "Module1", Mock.Of<ICredentials>());
            var config = new KubernetesConfig("image", CreatePodParameters.Create(hostConfig: HostPidModeHostConfig), Option.None<AuthConfig>());
            var docker = new DockerModule("module1", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, Core.Constants.DefaultStartupOrder, DefaultConfigurationInfo, EnvVarsDict);
            var module = new KubernetesModule(docker, config, EdgeletModuleOwner);
            var labels = new Dictionary<string, string>();
            var mapper = CreateMapper();

            var deployment = mapper.CreateDeployment(identity, module, labels);

            Assert.NotNull(deployment);
            Assert.Equal(true, deployment.Spec.Template.Spec.HostPID);
        }

        [Fact]
        public void ContainerPidModeDeploymentCreation()
        {
            var identity = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "Module1", Mock.Of<ICredentials>());
            var config = new KubernetesConfig("image", CreatePodParameters.Create(hostConfig: ContainerPidModeHostConfig), Option.None<AuthConfig>());
            var docker = new DockerModule("module1", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, Core.Constants.DefaultStartupOrder, DefaultConfigurationInfo, EnvVarsDict);
            var module = new KubernetesModule(docker, config, EdgeletModuleOwner);
            var labels = new Dictionary<string, string>();
            var mapper = CreateMapper();

            var deployment = mapper.CreateDeployment(identity, module, labels);

            Assert.NotNull(deployment);
            Assert.Equal(false, deployment.Spec.Template.Spec.HostPID);
        }

        static Dictionary<string, ResourceQuantity> proxyLimits = new Dictionary<string, ResourceQuantity>
        {
            ["cpu"] = new ResourceQuantity("20m"),
            ["memory"] = new ResourceQuantity("1000M"),
        };
        static Dictionary<string, ResourceQuantity> agentLimits = new Dictionary<string, ResourceQuantity>
        {
            ["cpu"] = new ResourceQuantity("150m"),
            ["memory"] = new ResourceQuantity("1500Mi"),
        };

        static V1ResourceRequirements proxyReqs = new V1ResourceRequirements(proxyLimits, proxyLimits);
        static V1ResourceRequirements agentReqs = new V1ResourceRequirements(agentLimits, agentLimits);

        static KubernetesDeploymentMapper CreateMapper(
          bool useMountSourceForVolumeName = false,
          string storageClassName = "",
          string proxyImagePullSecretName = null,
          bool runAsNonRoot = false,
          IDictionary<string, bool> experimentalFeatures = null,
          string agentConfigMapName = "agentConfigMapName",
          string agentConfigPath = "agentConfigPath",
          string agentConfigVolume = "agentConfigVolume")
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
                Option.Some(proxyReqs),
                Option.Maybe(agentConfigMapName),
                Option.Maybe(agentConfigPath),
                Option.Maybe(agentConfigVolume),
                Option.Some(agentReqs),
                PortMapServiceType.ClusterIP,
                useMountSourceForVolumeName,
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
