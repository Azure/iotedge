// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using k8s;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using Microsoft.Rest;

    public class KubernetesEnvironmentOperator : IKubernetesEnvironmentOperator
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

        public void Start(CancellationTokenSource shutdownCts) => this.StartListPods(shutdownCts);

        public void Stop()
        {
            // TODO do we need lock here?
            this.podWatch.ForEach(watch => watch.Dispose());
        }

        public void Dispose() => this.Stop();

        void StartListPods(CancellationTokenSource shutdownCts) =>
            this.client.ListNamespacedPodWithHttpMessagesAsync(this.deviceNamespace, watch: true)
                .ContinueWith(this.OnListPodsCompleted, shutdownCts);

        async Task OnListPodsCompleted(Task<HttpOperationResponse<V1PodList>> task, object shutdownCtsObject)
        {
            // The cts object is coming from an external source, check it and put it into an Option for safe handling.
            Option<CancellationTokenSource> shutdownCts = Option.Maybe(shutdownCtsObject as CancellationTokenSource);

            HttpOperationResponse<V1PodList> podListResp = await task;
            try
            {
                podListResp = await task;
            }
            catch (Exception ex)
            {
                Events.ListPodsFailed(ex);
                shutdownCts.ForEach(cts => cts.Cancel());
                throw;
            }

            try
            {
                this.podWatch = Option.Some(
                    podListResp.Watch<V1Pod, V1PodList>(
                        onEvent: (type, item) => this.PodOnEventHandlerAsync(type, item, shutdownCts),
                        onClosed: () => this.RestartWatch(shutdownCts),
                        onError: (ex) => this.HandleError(ex, shutdownCts)));
            }
            catch (Exception watchEx)
            {
                Events.ContinueTaskFailed(watchEx);
                shutdownCts.ForEach(cts => cts.Cancel());
                throw;
            }
        }

        internal void PodOnEventHandlerAsync(WatchEventType type, V1Pod item, Option<CancellationTokenSource> shutdownCts)
        {
            try
            {
                this.HandlePodChangedAsync(type, item);
            }
            catch (Exception ex)
            {
                Events.PodHandlerFailed(ex);
                if (ex.IsFatal())
                {
                    shutdownCts.ForEach(cts => cts.Cancel());
                    throw;
                }
            }
        }

        internal void RestartWatch(Option<CancellationTokenSource> shutdownCts)
        {
            Events.PodWatchClosed();

            // get rid of the current pod watch object since watch was closed.
            this.podWatch.ForEach(watch => watch.Dispose());
            this.podWatch = Option.None<Watcher<V1Pod>>();

            // kick off a new watch
            try
            {
                this.StartListPods(shutdownCts.OrDefault());
            }
            catch (Exception ex)
            {
                // Failure to start a new watch is a critical failure, request shutdown.
                Events.PodWatchRestartFailed(ex);
                shutdownCts.ForEach(cts => cts.Cancel());
                throw;
            }
        }

        internal void HandleError(Exception ex, Option<CancellationTokenSource> shutdownCts)
        {
            // Some unknown error happened while starting watch, request shutdown.
            Events.PodWatchFailed(ex);
            shutdownCts.ForEach(cts => cts.Cancel());
            throw ex;
        }

        void HandlePodChangedAsync(WatchEventType type, V1Pod pod)
        {
            // if the pod doesn't have the module label set then we are not interested in it
            if (!pod.Metadata.Labels.ContainsKey(Constants.K8sEdgeModuleLabel))
            {
                return;
            }

            Events.PodStatus(type, pod);
            switch (type)
            {
                case WatchEventType.Added:
                case WatchEventType.Modified:
                case WatchEventType.Error:
                    this.moduleStatusSource.CreateOrUpdateAddPodInfo(pod);
                    break;

                case WatchEventType.Deleted:
                    if (!this.moduleStatusSource.RemovePodInfo(pod))
                    {
                        Events.PodStatusRemoveError(pod);
                    }

                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }
    }

    static class Events
    {
        const int IdStart = KubernetesEventIds.KubernetesEnvironmentOperator;
        static readonly ILogger Log = Logger.Factory.CreateLogger<KubernetesEnvironmentOperator>();

        enum EventIds
        {
            WatchFailed = IdStart,
            HandlerFailed,
            PodStatus,
            PodStatusRemoveError,
            WatchClosed,
            WatchRestartFailed,
            ListPodsFailed,
            ContinueTaskFailed,
        }

        public static void PodWatchFailed(Exception ex)
        {
            Log.LogError((int)EventIds.WatchFailed, ex, "Error event in Pod Watch task, requesting shutdown.");
        }

        public static void PodHandlerFailed(Exception ex)
        {
            Log.LogError((int)EventIds.WatchFailed, ex, "Exception caught in Pod Watch Event Handler.");
        }

        public static void PodStatus(WatchEventType type, V1Pod pod)
        {
            Log.LogDebug((int)EventIds.PodStatus, $"Pod '{pod.Metadata.Name}', status'{type}'");
        }

        public static void PodStatusRemoveError(V1Pod pod)
        {
            Log.LogWarning((int)EventIds.PodStatusRemoveError, $"Notified of pod {pod.Metadata.Name} deleted, but not removed from our pod list");
        }

        public static void PodWatchClosed()
        {
            Log.LogInformation((int)EventIds.WatchClosed, $"K8s closed the pod watch. Attempting to reopen watch.");
        }

        public static void PodWatchRestartFailed(Exception ex)
        {
            Log.LogError((int)EventIds.WatchRestartFailed, ex, "Exception caught while attempting Pod Watch restart, requesting shutdown.");
        }

        public static void ListPodsFailed(Exception ex)
        {
            Log.LogError((int)EventIds.ListPodsFailed, ex, "Exception caught on pod list, requesting shutdown.");
        }

        public static void ContinueTaskFailed(Exception ex)
        {
            Log.LogError((int)EventIds.ContinueTaskFailed, ex, "Exception caught while setting up pod watch, requesting shutdown.");
        }
    }
}
