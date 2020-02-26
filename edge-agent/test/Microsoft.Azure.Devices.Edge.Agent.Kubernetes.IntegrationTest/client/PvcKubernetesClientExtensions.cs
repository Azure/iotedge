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

        public static async Task CreatePvcAsync(this KubernetesClient client, string persistentVolumeClaimName, Dictionary<string, string> labels)
        {
            var persistentVolumeClaim = new V1PersistentVolumeClaim
            {
                Metadata = new V1ObjectMeta(
                    name: persistentVolumeClaimName,
                    /*labels: new Dictionary<string, string>
                    {
                        [KubernetesConstants.K8sEdgeDeviceLabel] = KubeUtils.SanitizeLabelValue("deviceid"),
                        [KubernetesConstants.K8sEdgeHubNameLabel] = KubeUtils.SanitizeLabelValue("hostname")
                    },*/
                    labels: labels,
                    ownerReferences: new List<V1OwnerReference>
                    {
                        new V1OwnerReference("v1", name: "iotedged", kind: "Deployment", uid: "123")
                    }),
            };
            await client.Kubernetes.CreateNamespacedPersistentVolumeClaimAsync(persistentVolumeClaim, client.DeviceNamespace);
        }

        public static async Task DeletePvcAsync(this KubernetesClient client, string persistentVolumeClaimName) => await client.Kubernetes.DeleteNamespacedPersistentVolumeClaimAsync(persistentVolumeClaimName, client.DeviceNamespace);
    }
}
