// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment
{
    using System.Collections.Generic;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;

    public interface IKubernetesServiceMapper
    {
        Option<V1Service> CreateService(IModuleIdentity identity, KubernetesModule module, IDictionary<string, string> labels);

        void UpdateService(V1Service to, V1Service from);
    }
}
