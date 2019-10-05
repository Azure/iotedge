// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment
{
    using System.Collections.Generic;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;

    public interface IKubernetesServiceAccountMapper
    {
        V1ServiceAccount CreateServiceAccount(IModuleIdentity identity, IDictionary<string, string> labels);

        void UpdateServiceAccount(V1ServiceAccount to, V1ServiceAccount from);
    }
}
