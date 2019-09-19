// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment
{
    using System.Collections.Generic;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;

    public interface IKubernetesServiceProvider
    {
        Option<V1Service> GetService(IModuleIdentity identity, KubernetesModule module, IDictionary<string, string> labels);
    }
}
