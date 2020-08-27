// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Test.EdgeDeployment.Pvc
{
    using System.Collections.Generic;
    using System.Linq;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Microsoft.Azure.Devices.Edge.Agent.Docker.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment.Pvc;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;
    using KubernetesConstants = Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Constants;

    [Unit]
    public class KubernetesPvcMapperTest
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
                    Target = "/tmp/volumea"
                },
                new Mount
                {
                    Type = "volume",
                    ReadOnly = false,
                    Source = "b-volume",
                    Target = "/tmp/volumeb"
                }
            }
        };

        static readonly HostConfig VolumeMountHostConfig1 = new HostConfig
        {
            Mounts = new List<Mount>
            {
                new Mount
                {
                    Type = "volume",
                    ReadOnly = true,
                    Source = "a-volume",
                    Target = "/tmp/volumea"
                }
            }
        };

        static readonly HostConfig VolumeNullMount = new HostConfig
        {
            Mounts = null
        };

        static readonly Dictionary<string, string> DefaultLabels = new Dictionary<string, string>
        {
            [KubernetesConstants.K8sEdgeDeviceLabel] = KubeUtils.SanitizeLabelValue("device1"),
        };

        static readonly ResourceName ResourceName = new ResourceName("hostname", "deviceId");

        static readonly KubernetesModuleOwner EdgeletModuleOwner = new KubernetesModuleOwner("v1", "Deployment", "iotedged", "123");

        [Fact]
        public void NullMountsNoClaims()
        {
            var config = new KubernetesConfig("image", CreatePodParameters.Create(hostConfig: VolumeNullMount), Option.None<AuthConfig>());
            var docker = new DockerModule("module1", "v1", ModuleStatus.Running, RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, Constants.DefaultStartupOrder, DefaultConfigurationInfo, EnvVarsDict);
            var module = new KubernetesModule(docker, config, EdgeletModuleOwner);
            var mapper = new KubernetesPvcMapper(false, "storage", 1);

            var pvcs = mapper.CreatePersistentVolumeClaims(module, DefaultLabels);

            Assert.False(pvcs.HasValue);
        }

        [Fact]
        public void NoMountsNoClaims()
        {
            var config = new KubernetesConfig("image", CreatePodParameters.Create(), Option.None<AuthConfig>());
            var docker = new DockerModule("module1", "v1", ModuleStatus.Running, RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, Constants.DefaultStartupOrder, DefaultConfigurationInfo, EnvVarsDict);
            var module = new KubernetesModule(docker, config, EdgeletModuleOwner);
            var mapper = new KubernetesPvcMapper(false, "storage", 1);

            var pvcs = mapper.CreatePersistentVolumeClaims(module, DefaultLabels);

            Assert.False(pvcs.HasValue);
        }

        [Fact]
        public void EmptyDirMappingForVolume()
        {
            var config = new KubernetesConfig("image", CreatePodParameters.Create(hostConfig: VolumeMountHostConfig), Option.None<AuthConfig>());
            var docker = new DockerModule("module1", "v1", ModuleStatus.Running, RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, Constants.DefaultStartupOrder, DefaultConfigurationInfo, EnvVarsDict);
            var module = new KubernetesModule(docker, config, EdgeletModuleOwner);
            var mapper = new KubernetesPvcMapper(false, null, 0);

            var pvcs = mapper.CreatePersistentVolumeClaims(module, DefaultLabels);

            Assert.False(pvcs.HasValue);
        }

        [Fact]
        public void EmptyDirMappingForVolume2()
        {
            var config = new KubernetesConfig("image", CreatePodParameters.Create(hostConfig: VolumeMountHostConfig), Option.None<AuthConfig>());
            var docker = new DockerModule("module1", "v1", ModuleStatus.Running, RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, Constants.DefaultStartupOrder, DefaultConfigurationInfo, EnvVarsDict);
            var module = new KubernetesModule(docker, config, EdgeletModuleOwner);
            var mapper = new KubernetesPvcMapper(false, null, 0);

            var pvcs = mapper.CreatePersistentVolumeClaims(module, DefaultLabels);

            Assert.False(pvcs.HasValue);
        }

        [Fact]
        public void DefaultStorageClassMappingForVolume()
        {
            var config = new KubernetesConfig("image", CreatePodParameters.Create(hostConfig: VolumeMountHostConfig), Option.None<AuthConfig>());
            var docker = new DockerModule("module1", "v1", ModuleStatus.Running, RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, Constants.DefaultStartupOrder, DefaultConfigurationInfo, EnvVarsDict);
            var module = new KubernetesModule(docker, config, EdgeletModuleOwner);
            var mapper = new KubernetesPvcMapper(false, string.Empty, 10);
            var resourceQuantity = new ResourceQuantity("10Mi");

            var pvcs = mapper.CreatePersistentVolumeClaims(module, DefaultLabels);

            Assert.True(pvcs.HasValue);
            var pvcList = pvcs.OrDefault();
            Assert.True(pvcList.Any());
            Assert.Equal(2, pvcList.Count);

            var aVolumeClaim = pvcList.Single(pvc => pvc.Metadata.Name == "a-volume");
            Assert.True(aVolumeClaim.Metadata.Labels.SequenceEqual(DefaultLabels));
            Assert.Equal("ReadOnlyMany", aVolumeClaim.Spec.AccessModes[0]);
            Assert.Null(aVolumeClaim.Spec.VolumeName);
            Assert.Equal(string.Empty, aVolumeClaim.Spec.StorageClassName);
            Assert.Equal(resourceQuantity, aVolumeClaim.Spec.Resources.Requests["storage"]);
            Assert.Equal(1, aVolumeClaim.Metadata.OwnerReferences.Count);
            Assert.Equal(V1Deployment.KubeKind, aVolumeClaim.Metadata.OwnerReferences[0].Kind);
            Assert.Equal(EdgeletModuleOwner.Name, aVolumeClaim.Metadata.OwnerReferences[0].Name);

            var bVolumeClaim = pvcList.Single(pvc => pvc.Metadata.Name == "b-volume");
            Assert.True(bVolumeClaim.Metadata.Labels.SequenceEqual(DefaultLabels));
            Assert.Equal("ReadWriteMany", bVolumeClaim.Spec.AccessModes[0]);
            Assert.Null(bVolumeClaim.Spec.VolumeName);
            Assert.Equal(string.Empty, bVolumeClaim.Spec.StorageClassName);
            Assert.Equal(resourceQuantity, bVolumeClaim.Spec.Resources.Requests["storage"]);
            Assert.Equal(1, bVolumeClaim.Metadata.OwnerReferences.Count);
            Assert.Equal(V1Deployment.KubeKind, bVolumeClaim.Metadata.OwnerReferences[0].Kind);
            Assert.Equal(EdgeletModuleOwner.Name, bVolumeClaim.Metadata.OwnerReferences[0].Name);
        }

        [Fact]
        public void StorageClassMappingForVolume()
        {
            var config = new KubernetesConfig("image", CreatePodParameters.Create(hostConfig: VolumeMountHostConfig), Option.None<AuthConfig>());
            var docker = new DockerModule("module1", "v1", ModuleStatus.Running, RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, Constants.DefaultStartupOrder, DefaultConfigurationInfo, EnvVarsDict);
            var module = new KubernetesModule(docker, config, EdgeletModuleOwner);
            var mapper = new KubernetesPvcMapper(false, "default", 10);
            var resourceQuantity = new ResourceQuantity("10Mi");

            var pvcs = mapper.CreatePersistentVolumeClaims(module, DefaultLabels);

            Assert.True(pvcs.HasValue);
            var pvcList = pvcs.OrDefault();
            Assert.True(pvcList.Any());
            Assert.Equal(2, pvcList.Count);

            var aVolumeClaim = pvcList.Single(pvc => pvc.Metadata.Name == "a-volume");
            Assert.True(aVolumeClaim.Metadata.Labels.SequenceEqual(DefaultLabels));
            Assert.Equal("ReadOnlyMany", aVolumeClaim.Spec.AccessModes[0]);
            Assert.Null(aVolumeClaim.Spec.VolumeName);
            Assert.Equal("default", aVolumeClaim.Spec.StorageClassName);
            Assert.Equal(resourceQuantity, aVolumeClaim.Spec.Resources.Requests["storage"]);
            Assert.Equal(1, aVolumeClaim.Metadata.OwnerReferences.Count);
            Assert.Equal(V1Deployment.KubeKind, aVolumeClaim.Metadata.OwnerReferences[0].Kind);
            Assert.Equal(EdgeletModuleOwner.Name, aVolumeClaim.Metadata.OwnerReferences[0].Name);

            var bVolumeClaim = pvcList.Single(pvc => pvc.Metadata.Name == "b-volume");
            Assert.True(bVolumeClaim.Metadata.Labels.SequenceEqual(DefaultLabels));
            Assert.Equal("ReadWriteMany", bVolumeClaim.Spec.AccessModes[0]);
            Assert.Null(bVolumeClaim.Spec.VolumeName);
            Assert.Equal("default", bVolumeClaim.Spec.StorageClassName);
            Assert.Equal(resourceQuantity, bVolumeClaim.Spec.Resources.Requests["storage"]);
            Assert.Equal(1, bVolumeClaim.Metadata.OwnerReferences.Count);
            Assert.Equal(V1Deployment.KubeKind, bVolumeClaim.Metadata.OwnerReferences[0].Kind);
            Assert.Equal(EdgeletModuleOwner.Name, bVolumeClaim.Metadata.OwnerReferences[0].Name);
        }

        [Fact]
        public void VolumeNameMappingForVolume()
        {
            var config = new KubernetesConfig("image", CreatePodParameters.Create(hostConfig: VolumeMountHostConfig), Option.None<AuthConfig>());
            var docker = new DockerModule("module1", "v1", ModuleStatus.Running, RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, Constants.DefaultStartupOrder, DefaultConfigurationInfo, EnvVarsDict);
            var module = new KubernetesModule(docker, config, EdgeletModuleOwner);
            var mapper = new KubernetesPvcMapper(true, "storage-class", 37);
            var resourceQuantity = new ResourceQuantity("37Mi");

            var pvcs = mapper.CreatePersistentVolumeClaims(module, DefaultLabels);

            Assert.True(pvcs.HasValue);
            var pvcList = pvcs.OrDefault();
            Assert.True(pvcList.Any());
            Assert.Equal(2, pvcList.Count);

            var aVolumeClaim = pvcList.Single(pvc => pvc.Metadata.Name == "a-volume");
            Assert.True(aVolumeClaim.Metadata.Labels.SequenceEqual(DefaultLabels));
            Assert.Equal("ReadOnlyMany", aVolumeClaim.Spec.AccessModes[0]);
            Assert.Equal("storage-class", aVolumeClaim.Spec.StorageClassName);
            Assert.Equal("a-volume", aVolumeClaim.Spec.VolumeName);
            Assert.Equal(resourceQuantity, aVolumeClaim.Spec.Resources.Requests["storage"]);
            Assert.Equal(1, aVolumeClaim.Metadata.OwnerReferences.Count);
            Assert.Equal(V1Deployment.KubeKind, aVolumeClaim.Metadata.OwnerReferences[0].Kind);
            Assert.Equal(EdgeletModuleOwner.Name, aVolumeClaim.Metadata.OwnerReferences[0].Name);

            var bVolumeClaim = pvcList.Single(pvc => pvc.Metadata.Name == "b-volume");
            Assert.True(bVolumeClaim.Metadata.Labels.SequenceEqual(DefaultLabels));
            Assert.Equal("ReadWriteMany", bVolumeClaim.Spec.AccessModes[0]);
            Assert.Equal("storage-class", bVolumeClaim.Spec.StorageClassName);
            Assert.Equal("b-volume", bVolumeClaim.Spec.VolumeName);
            Assert.Equal(resourceQuantity, bVolumeClaim.Spec.Resources.Requests["storage"]);
            Assert.Equal(1, bVolumeClaim.Metadata.OwnerReferences.Count);
            Assert.Equal(V1Deployment.KubeKind, bVolumeClaim.Metadata.OwnerReferences[0].Kind);
            Assert.Equal(EdgeletModuleOwner.Name, bVolumeClaim.Metadata.OwnerReferences[0].Name);
        }

        [Fact]
        public void PreferVolumeNameMappingForVolume()
        {
            var config = new KubernetesConfig("image", CreatePodParameters.Create(hostConfig: VolumeMountHostConfig1), Option.None<AuthConfig>());
            var docker = new DockerModule("module1", "v1", ModuleStatus.Running, RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, Constants.DefaultStartupOrder, DefaultConfigurationInfo, EnvVarsDict);
            var module = new KubernetesModule(docker, config, EdgeletModuleOwner);
            var mapper = new KubernetesPvcMapper(true, "storageclass", 1);
            var resourceQuantity = new ResourceQuantity("1Mi");

            var pvcs = mapper.CreatePersistentVolumeClaims(module, DefaultLabels);

            Assert.True(pvcs.HasValue);
            var pvcList = pvcs.OrDefault();
            Assert.True(pvcList.Any());
            Assert.Single(pvcList);

            var aVolumeClaim = pvcList.Single(pvc => pvc.Metadata.Name == "a-volume");
            Assert.True(aVolumeClaim.Metadata.Labels.SequenceEqual(DefaultLabels));
            Assert.Equal("ReadOnlyMany", aVolumeClaim.Spec.AccessModes[0]);
            Assert.Equal("storageclass", aVolumeClaim.Spec.StorageClassName);
            Assert.Equal("a-volume", aVolumeClaim.Spec.VolumeName);
            Assert.Equal(resourceQuantity, aVolumeClaim.Spec.Resources.Requests["storage"]);
            Assert.Equal(1, aVolumeClaim.Metadata.OwnerReferences.Count);
            Assert.Equal(V1Deployment.KubeKind, aVolumeClaim.Metadata.OwnerReferences[0].Kind);
            Assert.Equal(EdgeletModuleOwner.Name, aVolumeClaim.Metadata.OwnerReferences[0].Name);
        }
    }
}
