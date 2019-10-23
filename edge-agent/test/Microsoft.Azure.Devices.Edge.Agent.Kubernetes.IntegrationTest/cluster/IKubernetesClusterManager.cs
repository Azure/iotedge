// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.IntegrationTest.Cluster
{
    using System.Threading.Tasks;

    public interface IKubernetesClusterManager : IKubernetesClientProvider
    {
        Task Create();

        Task Delete();
    }
}
