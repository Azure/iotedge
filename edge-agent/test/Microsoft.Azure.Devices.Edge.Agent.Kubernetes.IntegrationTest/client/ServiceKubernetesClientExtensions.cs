// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.IntegrationTest.Client
{
    using System.Threading.Tasks;
    using k8s;
    using k8s.Models;

    public static class ServiceKubernetesClientExtensions
    {
        public static async Task<V1ServiceList> ListServicesAsync(this KubernetesClient client, string deviceSelector) => await client.Kubernetes.ListNamespacedServiceAsync(client.DeviceNamespace, labelSelector: deviceSelector);
    }
}
