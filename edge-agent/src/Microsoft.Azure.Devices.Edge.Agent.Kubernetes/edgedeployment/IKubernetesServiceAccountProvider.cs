// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment
{
    using System.Collections.Generic;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;

    public interface IKubernetesServiceAccountProvider
    {
        V1ServiceAccount GetServiceAccount(IModuleIdentity identity, KubernetesModule module, IDictionary<string, string> labels);

        void Update(V1ServiceAccount to, V1ServiceAccount from);
    }
}
