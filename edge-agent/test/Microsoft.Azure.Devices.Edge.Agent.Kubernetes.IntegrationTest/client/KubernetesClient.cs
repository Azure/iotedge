// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.IntegrationTest.Client
{
    using System;
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
    }
}
