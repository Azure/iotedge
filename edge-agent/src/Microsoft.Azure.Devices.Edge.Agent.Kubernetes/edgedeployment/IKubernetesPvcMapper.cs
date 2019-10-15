// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment
{
    using System.Collections.Generic;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Util;

    public interface IKubernetesPvcMapper
    {
        Option<List<V1PersistentVolumeClaim>> CreatePersistentVolumeClaims(KubernetesModule module, IDictionary<string, string> labels);

        void UpdatePersistentVolumeClaim(V1PersistentVolumeClaim to, V1PersistentVolumeClaim from);
    }
}
