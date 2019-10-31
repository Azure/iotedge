// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment.ServiceAccount
{
    using System.Collections.Generic;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using KubernetesConstants = Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Constants;

    public class KubernetesServiceAccountMapper : IKubernetesServiceAccountMapper
    {
        public V1ServiceAccount CreateServiceAccount(IModuleIdentity identity, IDictionary<string, string> labels, EdgeDeploymentDefinition edgeDeploymentDefinition)
        {
            string name = identity.DeploymentName();
            var annotations = new Dictionary<string, string>
            {
                [KubernetesConstants.K8sEdgeOriginalModuleId] = ModuleIdentityHelper.GetModuleName(identity.ModuleId)
            };

            var ownerReferences = new List<V1OwnerReference>
            {
                new V1OwnerReference(
                    apiVersion: edgeDeploymentDefinition.ApiVersion,
                    kind: edgeDeploymentDefinition.Kind,
                    name: edgeDeploymentDefinition.Metadata.Name,
                    uid: edgeDeploymentDefinition.Metadata.Uid,
                    blockOwnerDeletion: true)
            };
            var metadata = new V1ObjectMeta(annotations, name: name, labels: labels, ownerReferences: ownerReferences);
            return new V1ServiceAccount(metadata: metadata);
        }

        public void UpdateServiceAccount(V1ServiceAccount to, V1ServiceAccount from)
        {
            to.Metadata.ResourceVersion = from.Metadata.ResourceVersion;
        }
    }
}
