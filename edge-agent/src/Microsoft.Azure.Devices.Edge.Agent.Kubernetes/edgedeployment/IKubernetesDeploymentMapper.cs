// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment
{
    using System.Collections.Generic;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;

    public interface IKubernetesDeploymentMapper
    {
        V1Deployment CreateDeployment(IModuleIdentity identity, KubernetesModule module, IDictionary<string, string> labels);

        void UpdateDeployment(V1Deployment to, V1Deployment from);
    }
}
