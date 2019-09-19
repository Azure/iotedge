// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment
{
    using System.Collections.Generic;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using KubernetesConstants = Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Constants;

    public class KubernetesServiceAccountProvider : IKubernetesServiceAccountProvider
    {
        public V1ServiceAccount GetServiceAccount(IModuleIdentity identity, KubernetesModule module, IDictionary<string, string> labels)
        {
            string name = KubeUtils.SanitizeK8sValue(identity.ModuleId);
            var annotations = new Dictionary<string, string>
            {
                [KubernetesConstants.K8sEdgeOriginalModuleId] = ModuleIdentityHelper.GetModuleName(identity.ModuleId)
            };
            var metadata = new V1ObjectMeta(annotations, name: name, labels: labels);
            return new V1ServiceAccount(metadata: metadata);
        }
    }
}
