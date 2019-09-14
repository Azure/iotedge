// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    using k8s.Models;

    public interface IRuntimeInfoSource
    {
        void CreateOrUpdateAddPodInfo(string podName, V1Pod pod);

        bool RemovePodInfo(string podName);
    }
}
