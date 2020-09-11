// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Test
{
    using System.Collections.Generic;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment.ServiceAccount;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;
    using KubernetesConstants = Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Constants;

    [Unit]
    public class KubernetesServiceAccountMapperTest
    {
        static readonly ResourceName ResourceName = new ResourceName("hostname", "deviceId");

        static readonly KubernetesModuleOwner EdgeletModuleOwner = new KubernetesModuleOwner("v1", "Deployment", "iotedged", "123");

        [Fact]
        public void RequiredMetadataExistsWhenCreated()
        {
            var identity = new ModuleIdentity("hostname", "deviceid", "ModuleId", Mock.Of<ICredentials>());
            var mapper = new KubernetesServiceAccountMapper();
            var labels = new Dictionary<string, string> { ["device"] = "k8s-device" };
            var config = new KubernetesConfig("image", CreatePodParameters.Create(labels: labels), Option.None<AuthConfig>());
            var configurationInfo = new ConfigurationInfo("1");
            var envVarsDict = new Dictionary<string, EnvVal>();

            var dockerConfig = new DockerConfig("test-image:1");

            var docker = new DockerModule("module1", "v1", ModuleStatus.Running, RestartPolicy.Always, dockerConfig, ImagePullPolicy.OnCreate, Constants.DefaultStartupOrder, configurationInfo, envVarsDict);
            var module = new KubernetesModule(docker, config, EdgeletModuleOwner);

            var serviceAccount = mapper.CreateServiceAccount(module, identity, labels);

            Assert.NotNull(serviceAccount);
            Assert.Equal("moduleid", serviceAccount.Metadata.Name);
            Assert.Equal(1, serviceAccount.Metadata.Annotations.Count);
            Assert.Equal("ModuleId", serviceAccount.Metadata.Annotations[KubernetesConstants.K8sEdgeOriginalModuleId]);
            Assert.Equal(1, serviceAccount.Metadata.Labels.Count);
            Assert.Equal("k8s-device", serviceAccount.Metadata.Labels["device"]);
            Assert.Equal(1, serviceAccount.Metadata.OwnerReferences.Count);
            Assert.Equal(V1Deployment.KubeKind, serviceAccount.Metadata.OwnerReferences[0].Kind);
            Assert.Equal(EdgeletModuleOwner.Name, serviceAccount.Metadata.OwnerReferences[0].Name);
        }
    }
}
