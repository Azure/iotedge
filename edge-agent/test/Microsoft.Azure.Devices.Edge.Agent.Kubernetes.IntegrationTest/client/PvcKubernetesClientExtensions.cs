// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.IntegrationTest.Client
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using k8s;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Util;
    using KubernetesConstants = Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Constants;

    public static class PvcKubernetesClientExtensions
    {
        public static async Task<Option<V1PersistentVolumeClaimList>> WaitUntilAnyPersistentVolumeClaimAsync(this KubernetesClient client, CancellationToken token) =>
           await KubernetesClient.WaitUntilAsync(
               () => client.Kubernetes.ListNamespacedPersistentVolumeClaimAsync(client.DeviceNamespace, cancellationToken: token),
               p => p.Items.Any(),
               token);

        public static async Task<V1PersistentVolumeClaimList> ListPeristentVolumeClaimsAsync(this KubernetesClient client) => await client.Kubernetes.ListNamespacedPersistentVolumeClaimAsync(client.DeviceNamespace);
    }
}
