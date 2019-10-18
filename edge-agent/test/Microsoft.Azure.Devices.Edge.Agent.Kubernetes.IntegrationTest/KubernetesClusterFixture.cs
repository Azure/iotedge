// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.IntegrationTest
{
    using System;
    using System.Threading.Tasks;
    using k8s;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.IntegrationTest.cluster;
    using Xunit;

    public class KubernetesClusterFixture : IAsyncLifetime
    {
        readonly IKubernetesClientProvider clientProvider;

        public KubernetesClusterFixture()
        {
            this.clientProvider = new KindClusterManager($"ea-{Guid.NewGuid()}");
            // this.clientProvider = new KindKubernetesClientProvider("ea-edgy");
        }

        public async Task InitializeAsync()
        {
            if (this.clientProvider is IKubernetesClusterManager clusterManager)
            {
                await clusterManager.Create();
            }

            this.Client = await this.clientProvider.GetClient();
        }

        public async Task DisposeAsync()
        {
            if (this.clientProvider is IKubernetesClusterManager clusterManager)
            {
                await clusterManager.Delete();
            }
        }

        public IKubernetes Client { get; private set; }
    }
}
