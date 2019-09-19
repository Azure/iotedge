// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment
{
    using System.Collections.Generic;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;

    public interface IKubernetesDeploymentProvider
    {
        V1Deployment GetDeployment(IModuleIdentity identity, KubernetesModule module, IDictionary<string, string> labels);
    }
}
