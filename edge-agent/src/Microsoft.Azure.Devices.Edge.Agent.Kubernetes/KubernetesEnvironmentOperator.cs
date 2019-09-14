// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    using System;
    using System.Threading.Tasks;
    using k8s;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using Microsoft.Rest;

    public class KubernetesEnvironmentOperator : IKubernetesOperator
    {
        readonly IRuntimeInfoSource moduleStatusSource;
        readonly IKubernetes client;
        readonly string deviceNamespace;
        Option<Watcher<V1Pod>> podWatch;

        public KubernetesEnvironmentOperator(
            string deviceNamespace,
            IRuntimeInfoSource moduleStatusSource,
            IKubernetes client)
        {
            this.deviceNamespace = deviceNamespace;
            this.moduleStatusSource = moduleStatusSource;
            this.client = Preconditions.CheckNotNull(client, nameof(client));
            this.podWatch = Option.None<Watcher<V1Pod>>();
        }

        public void Start() => this.StartListPods();

        public void Stop()
        {
            // TODO do we need lock here?
            this.podWatch.ForEach(watch => watch.Dispose());
        }

        public void Dispose() => this.Stop();

        void StartListPods() =>
            this.client.ListNamespacedPodWithHttpMessagesAsync(this.deviceNamespace, watch: true)
                .ContinueWith(this.OnListPodsCompleted);

        async Task OnListPodsCompleted(Task<HttpOperationResponse<V1PodList>> task)
        {
            HttpOperationResponse<V1PodList> podListResp = await task;

            this.podWatch = Option.Some(
                podListResp.Watch<V1Pod>(
                    onEvent: (type, item) =>
                    {
                        try
                        {
                            this.HandlePodChangedAsync(type, item);
                        }
                        catch (Exception ex) when (!ex.IsFatal())
                        {
                            Events.ExceptionInPodWatch(ex);
                        }
                    },
                    onClosed: () =>
                    {
                        Events.PodWatchClosed();

                        // get rid of the current pod watch object since we got closed
                        this.podWatch.ForEach(watch => watch.Dispose());
                        this.podWatch = Option.None<Watcher<V1Pod>>();

                        // kick off a new watch
                        this.Start();
                    },
                    onError: Events.ExceptionInPodWatch));
        }

        void HandlePodChangedAsync(WatchEventType type, V1Pod pod)
        {
            // if the pod doesn't have the module label set then we are not interested in it
            if (!pod.Metadata.Labels.TryGetValue(Constants.K8sEdgeModuleLabel, out string podName))
            {
                return;
            }

            Events.PodStatus(type, podName);
            switch (type)
            {
                case WatchEventType.Added:
                case WatchEventType.Modified:
                case WatchEventType.Error:
                    this.moduleStatusSource.CreateOrUpdateAddPodInfo(podName, pod);
                    break;

                case WatchEventType.Deleted:
                    if (!this.moduleStatusSource.RemovePodInfo(podName))
                    {
                        Events.PodStatusRemoveError(podName);
                    }
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }
    }

    static class Events
    {
        const int IdStart = KubernetesEventIds.KubernetesRuntimeInfoProvider;
        static readonly ILogger Log = Logger.Factory.CreateLogger<KubernetesEnvironmentOperator>();

        enum EventIds
        {
            InvalidModuleType = IdStart,
            ExceptionInPodWatch,
            PodStatus,
            PodStatusRemoveError,
            PodWatchClosed,
            NullListResponse,
            NullNodeInfoResponse,
        }

        public static void ExceptionInPodWatch(Exception ex)
        {
            Log.LogError((int)EventIds.ExceptionInPodWatch, ex, "Exception caught in Pod Watch task.");
        }

        public static void PodStatus(WatchEventType type, string podname)
        {
            Log.LogDebug((int)EventIds.PodStatus, $"Pod '{podname}', status'{type}'");
        }

        public static void PodStatusRemoveError(string podname)
        {
            Log.LogWarning((int)EventIds.PodStatusRemoveError, $"Notified of pod {podname} deleted, but not removed from our pod list");
        }

        public static void NullListResponse(string listType, string what)
        {
            Log.LogError((int)EventIds.NullListResponse, $"{listType} returned null {what}");
        }

        public static void NullNodeInfoResponse(string nodeName)
        {
            Log.LogError((int)EventIds.NullNodeInfoResponse, $"node {nodeName} had no node information");
        }

        public static void PodWatchClosed()
        {
            Log.LogInformation((int)EventIds.PodWatchClosed, $"K8s closed the pod watch. Attempting to reopen watch.");
        }
    }
}
