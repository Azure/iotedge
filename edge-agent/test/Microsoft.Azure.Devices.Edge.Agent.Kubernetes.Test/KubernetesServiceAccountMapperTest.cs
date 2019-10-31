// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Test
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment.ServiceAccount;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;
    using KubernetesConstants = Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Constants;

    [Unit]
    public class KubernetesServiceAccountMapperTest
    {
        static readonly ResourceName ResourceName = new ResourceName("hostname", "deviceId");

        static readonly EdgeDeploymentDefinition EdgeDeploymentDefinition = new EdgeDeploymentDefinition("v1", "EdgeDeployment", new k8s.Models.V1ObjectMeta(name: ResourceName), new List<KubernetesModule>(), null);

        [Fact]
        public void RequiredMetadataExistsWhenCreated()
        {
            var identity = new ModuleIdentity("hostname", "gatewayhost", "deviceid", "ModuleId", Mock.Of<ICredentials>());
            var mapper = new KubernetesServiceAccountMapper();
            var labels = new Dictionary<string, string> { ["device"] = "k8s-device" };
            var edgeDefinition = new EdgeDeploymentDefinition("v1", "EdgeDeployment", new k8s.Models.V1ObjectMeta(name: ResourceName), new List<KubernetesModule>(), null);

            var serviceAccount = mapper.CreateServiceAccount(identity, labels, EdgeDeploymentDefinition);

            Assert.NotNull(serviceAccount);
            Assert.Equal("moduleid", serviceAccount.Metadata.Name);
            Assert.Equal(1, serviceAccount.Metadata.Annotations.Count);
            Assert.Equal("ModuleId", serviceAccount.Metadata.Annotations[KubernetesConstants.K8sEdgeOriginalModuleId]);
            Assert.Equal(1, serviceAccount.Metadata.Labels.Count);
            Assert.Equal("k8s-device", serviceAccount.Metadata.Labels["device"]);
            Assert.Equal(1, serviceAccount.Metadata.OwnerReferences.Count);
            Assert.Equal(KubernetesConstants.EdgeDeployment.Kind, serviceAccount.Metadata.OwnerReferences[0].Kind);
            Assert.Equal(ResourceName, serviceAccount.Metadata.OwnerReferences[0].Name);
        }
    }
}
