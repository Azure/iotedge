// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.IntegrationTest.Client
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using k8s;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Util;

    public class KubernetesClient
    {
        public string DeviceNamespace { get; }

        public IKubernetes Kubernetes { get; }

        public KubernetesClient(string deviceNamespace, IKubernetes client)
        {
            this.DeviceNamespace = deviceNamespace;
            this.Kubernetes = client;
        }

        public async Task<Option<V1PodList>> WaitUntilAnyPodsAsync(string fieldSelector, CancellationToken token) =>
            await WaitUntilAsync(
                () => this.Kubernetes.ListNamespacedPodAsync(this.DeviceNamespace, fieldSelector: fieldSelector, cancellationToken: token),
                pods => pods.Items.Any(),
                token);

        public async Task<Option<V1PodList>> WaitUntilPodsExactNumberAsync(int count, CancellationToken token) =>
            await WaitUntilAsync(
                () => this.Kubernetes.ListNamespacedPodAsync(this.DeviceNamespace, cancellationToken: token),
                pods => pods.Items.Count == count,
                token);

        public async Task<Option<V1ServiceAccountList>> WaitUntilAnyServiceAccountAsync(CancellationToken token) =>
           await WaitUntilAsync(
               () => this.Kubernetes.ListNamespacedServiceAccountAsync(this.DeviceNamespace, cancellationToken: token),
               sa => sa.Items.Any(),
               token);

        public async Task<Option<V1DeploymentList>> WaitUntilAnyDeploymentsAsync(CancellationToken token) =>
           await WaitUntilAsync(
               () => this.Kubernetes.ListNamespacedDeploymentAsync(this.DeviceNamespace, cancellationToken: token),
               d => d.Items.Any(),
               token);

        public async Task<Option<V1PersistentVolumeClaimList>> WaitUntilAnyPersistentVolumeClaim(CancellationToken token) =>
           await WaitUntilAsync(
               () => this.Kubernetes.ListNamespacedPersistentVolumeClaimAsync(this.DeviceNamespace, cancellationToken: token),
               p => p.Items.Any(),
               token);

        public static async Task<Option<T>> WaitUntilAsync<T>(Func<Task<T>> action, Func<T, bool> predicate, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                T result = await action();
                if (predicate(result))
                {
                    return Option.Some(result);
                }

                await Task.Delay(TimeSpan.FromMilliseconds(100), token);
            }

            return Option.None<T>();
        }

        internal Task<V1DeploymentList> ListDeployments(string deviceSelector) => this.Kubernetes.ListNamespacedDeploymentAsync(this.DeviceNamespace, labelSelector: deviceSelector);

        internal Task<V1ServiceAccountList> ListServiceAccounts(string deviceSelector) => this.Kubernetes.ListNamespacedServiceAccountAsync(this.DeviceNamespace, labelSelector: deviceSelector);

        internal Task<V1ServiceList> ListServices(string deviceSelector) => this.Kubernetes.ListNamespacedServiceAsync(this.DeviceNamespace, labelSelector: deviceSelector);

        internal Task<V1PersistentVolumeClaimList> ListPeristentVolumeClaims() => this.Kubernetes.ListNamespacedPersistentVolumeClaimAsync(this.DeviceNamespace);

        internal void DeleteDeployment(string moduleName) => this.Kubernetes.DeleteNamespacedDeployment(moduleName, this.DeviceNamespace);

        internal V1Status DeleteServiceAccount(string moduleName) => this.Kubernetes.DeleteNamespacedServiceAccount(moduleName, this.DeviceNamespace);

        internal void DeleteService(string moduleName) => this.Kubernetes.DeleteNamespacedService(moduleName, this.DeviceNamespace);

        internal void DeletePvc(string persistentVolumeClaimName) => this.Kubernetes.DeleteNamespacedPersistentVolumeClaim(persistentVolumeClaimName, this.DeviceNamespace);
    }
}
