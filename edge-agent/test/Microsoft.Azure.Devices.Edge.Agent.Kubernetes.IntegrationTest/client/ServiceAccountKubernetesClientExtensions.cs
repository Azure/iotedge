// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.IntegrationTest.Client
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using k8s;
    using k8s.Models;

    public static class ServiceAccountKubernetesClientExtensions
    {
        public static async Task AddModuleServiceAccountAsync(this KubernetesClient client, string name, IDictionary<string, string> labels, IDictionary<string, string> annotations)
        {
            var serviceAccount = new V1ServiceAccount
            {
                Metadata = new V1ObjectMeta
                {
                    Name = name,
                    Annotations = annotations,
                    Labels = labels
                }
            };

            await client.Kubernetes.CreateNamespacedServiceAccountAsync(serviceAccount, client.DeviceNamespace);
        }

        public static async Task<V1ServiceAccountList> ListServiceAccountsAsync(this KubernetesClient client, string deviceSelector) => await client.Kubernetes.ListNamespacedServiceAccountAsync(client.DeviceNamespace, labelSelector: deviceSelector);
    }
}
