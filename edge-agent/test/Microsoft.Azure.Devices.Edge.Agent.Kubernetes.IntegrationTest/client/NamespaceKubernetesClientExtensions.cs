// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.IntegrationTest.Client
{
    using System.Threading.Tasks;
    using k8s;
    using k8s.Models;

    public static class NamespaceKubernetesClientExtensions
    {
        public static async Task AddNamespaceAsync(this KubernetesClient client)
        {
            var @namespace = new V1Namespace
            {
                Metadata = new V1ObjectMeta
                {
                    Name = client.DeviceNamespace
                }
            };

            await client.Kubernetes.CreateNamespaceAsync(@namespace);
        }
    }
}
