// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.IntegrationTest.Cluster
{
    using System.Threading.Tasks;
    using k8s;

    public interface IKubernetesClientProvider
    {
        Task<IKubernetes> GetClientAsync();
    }
}
