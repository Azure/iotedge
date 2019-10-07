// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Test.Edgedeployment.Pvc
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using global::Docker.DotNet.Models;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment.Pvc;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Moq;
    using Xunit;

    using AgentModels = global::Microsoft.Azure.Devices.Edge.Agent.Docker.Models;
    using KubernetesConstants = Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Constants;

    [Unit]
    public class KubernetesPvcMapperTest
    {
        static readonly ConfigurationInfo DefaultConfigurationInfo = new ConfigurationInfo("1");
        static readonly IDictionary<string, EnvVal> EnvVarsDict = new Dictionary<string, EnvVal>();
        static readonly DockerConfig Config1 = new DockerConfig("test-image:1");
        static readonly AgentModels.HostConfig VolumeMountHostConfig = new AgentModels.HostConfig
        {
            Mounts = new List<AgentModels.Mount>
            {
                new AgentModels.Mount()
                {
                    Type = "volume",
                    ReadOnly = true,
                    Source = "a-volume",
                    Target = "/tmp/volumea"
                },
                new AgentModels.Mount()
                {
                    Type = "volume",
                    ReadOnly = false,
                    Source = "b-volume",
                    Target = "/tmp/volumeb"
                }
            }
        };
        static readonly Dictionary<string, string> DefaultLabels = new Dictionary<string, string>
        {
            [KubernetesConstants.K8sEdgeDeviceLabel] = KubeUtils.SanitizeLabelValue("device1"),
            [KubernetesConstants.K8sEdgeHubNameLabel] = KubeUtils.SanitizeLabelValue("hostname")
        };

        [Fact]
        public void EmptyDirMappingForVolume()
        {
            var config = new CombinedDockerConfig("image", new AgentModels.CreateContainerParameters(), Option.None<AuthConfig>());
            config.CreateOptions.HostConfig = VolumeMountHostConfig;
            var docker = new DockerModule("module1", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVarsDict);
            var module = new KubernetesModule(docker, config);
            var mapper = new KubernetesPvcMapper(string.Empty, string.Empty, 0);

            var pvcs = mapper.CreatePersistentVolumeClaims(module, DefaultLabels);

            Assert.True(pvcs.HasValue);
            var pvcList = pvcs.OrDefault();
            Assert.False(pvcList.Any());
        }

        [Fact]
        public void StorageClassMappingForVolume()
        {
            var config = new CombinedDockerConfig("image", new AgentModels.CreateContainerParameters(), Option.None<AuthConfig>());
            config.CreateOptions.HostConfig = VolumeMountHostConfig;
            var docker = new DockerModule("module1", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVarsDict);
            var module = new KubernetesModule(docker, config);
            var mapper = new KubernetesPvcMapper(string.Empty, "default", 10);
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

            var bVolumeClaim = pvcList.Single(pvc => pvc.Metadata.Name == "b-volume");
            Assert.True(bVolumeClaim.Metadata.Labels.SequenceEqual(DefaultLabels));
            Assert.Equal("ReadWriteMany", bVolumeClaim.Spec.AccessModes[0]);
            Assert.Null(bVolumeClaim.Spec.VolumeName);
            Assert.Equal("default", bVolumeClaim.Spec.StorageClassName);
            Assert.Equal(resourceQuantity, bVolumeClaim.Spec.Resources.Requests["storage"]);
        }

        [Fact]
        public void VolumeNameMappingForVolume()
        {
            var config = new CombinedDockerConfig("image", new AgentModels.CreateContainerParameters(), Option.None<AuthConfig>());
            config.CreateOptions.HostConfig = VolumeMountHostConfig;
            var docker = new DockerModule("module1", "v1", ModuleStatus.Running, Core.RestartPolicy.Always, Config1, ImagePullPolicy.OnCreate, DefaultConfigurationInfo, EnvVarsDict);
            var module = new KubernetesModule(docker, config);
            var mapper = new KubernetesPvcMapper("a-pvc-name", string.Empty, 37);
            var resourceQuantity = new ResourceQuantity("37Mi");

            var pvcs = mapper.CreatePersistentVolumeClaims(module, DefaultLabels);

            Assert.True(pvcs.HasValue);
            var pvcList = pvcs.OrDefault();
            Assert.True(pvcList.Any());
            Assert.Equal(2, pvcList.Count);

            var aVolumeClaim = pvcList.Single(pvc => pvc.Metadata.Name == "a-volume");
            Assert.True(aVolumeClaim.Metadata.Labels.SequenceEqual(DefaultLabels));
            Assert.Equal("ReadOnlyMany", aVolumeClaim.Spec.AccessModes[0]);
            Assert.Null(aVolumeClaim.Spec.StorageClassName);
            Assert.Equal("a-pvc-name", aVolumeClaim.Spec.VolumeName);
            Assert.Equal(resourceQuantity, aVolumeClaim.Spec.Resources.Requests["storage"]);

            var bVolumeClaim = pvcList.Single(pvc => pvc.Metadata.Name == "b-volume");
            Assert.True(bVolumeClaim.Metadata.Labels.SequenceEqual(DefaultLabels));
            Assert.Equal("ReadWriteMany", bVolumeClaim.Spec.AccessModes[0]);
            Assert.Null(bVolumeClaim.Spec.StorageClassName);
            Assert.Equal("a-pvc-name", bVolumeClaim.Spec.VolumeName);
            Assert.Equal(resourceQuantity, bVolumeClaim.Spec.Resources.Requests["storage"]);
        }
    }
}
