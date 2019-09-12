// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using k8s;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Extensions.Logging;
    using Microsoft.Rest;

    using AgentDocker = Microsoft.Azure.Devices.Edge.Agent.Docker;

    public class KubernetesRuntimeInfoProvider : IKubernetesOperator, IRuntimeInfoProvider, INotifyPropertyChanged
    {
        readonly string deviceNamespace;
        readonly IKubernetes client;
        readonly Dictionary<string, ModuleRuntimeInfo> moduleRuntimeInfos;
        readonly AsyncLock moduleLock;
        Option<Watcher<V1Pod>> podWatch;

        public KubernetesRuntimeInfoProvider(string deviceNamespace, IKubernetes client)
        {
            this.deviceNamespace = Preconditions.CheckNonWhiteSpace(deviceNamespace, nameof(deviceNamespace));
            this.client = Preconditions.CheckNotNull(client, nameof(client));

            this.podWatch = Option.None<Watcher<V1Pod>>();
            this.moduleRuntimeInfos = new Dictionary<string, ModuleRuntimeInfo>();
            this.moduleLock = new AsyncLock();
        }

        private async Task ListPodComplete(Task<HttpOperationResponse<V1PodList>> podListRespTask)
        {
            if (podListRespTask != null)
            {
                HttpOperationResponse<V1PodList> podListResp = await podListRespTask;
                if (podListResp != null)
                {
                    this.podWatch = Option.Some(
                        podListResp.Watch<V1Pod>(
                            onEvent: async (type, item) =>
                            {
                                try
                                {
                                    await this.WatchPodEventsAsync(type, item);
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
                                this.client.ListNamespacedPodWithHttpMessagesAsync(this.deviceNamespace, watch: true).ContinueWith(this.ListPodComplete);
                            },
                            onError: Events.ExceptionInPodWatch));
                }
                else
                {
                    Events.NullListResponse("ListNamespacedPodWithHttpMessagesAsync", "http response");
                    throw new KuberenetesResponseException("Null response from ListNamespacedPodWithHttpMessagesAsync");
                }
            }
            else
            {
                Events.NullListResponse("ListNamespacedPodWithHttpMessagesAsync", "task");
                throw new KuberenetesResponseException("Null Task from ListNamespacedPodWithHttpMessagesAsync");
            }
        }

        public void Start() => this.client.ListNamespacedPodWithHttpMessagesAsync(this.deviceNamespace, watch: true).ContinueWith(this.ListPodComplete);

        public void Stop() => this.podWatch.ForEach(watch => watch.Dispose());

        public void Dispose() => this.Stop();

        public async Task<IEnumerable<ModuleRuntimeInfo>> GetModules(CancellationToken cancellationToken)
        {
            using (await this.moduleLock.LockAsync(cancellationToken))
            {
                return this.moduleRuntimeInfos.Select(kvp => kvp.Value).ToList();
            }
        }

        public async Task<Stream> GetModuleLogs(string module, bool follow, Option<int> tail, Option<int> since, CancellationToken cancellationToken)
        {
            int tailLines = tail.OrDefault();
            int sinceSec = since.OrDefault();

            return await this.client.ReadNamespacedPodLogAsync(
                module,
                Constants.K8sNamespace,
                follow: follow,
                tailLines: tailLines == default(int) ? default(int?) : tailLines,
                sinceSeconds: sinceSec == default(int) ? default(int?) : sinceSec,
                cancellationToken: cancellationToken);
        }

        public Task<SystemInfo> GetSystemInfo()
        {
            // Comment this out to unblock testing.
            // V1NodeList k8SNodes = await this.client.ListNodeAsync();
            // string osType = string.Empty;
            // string arch = string.Empty;
            // string version = string.Empty;
            // if (k8SNodes.Items != null)
            // {
            //     V1Node firstNode = k8SNodes.Items.FirstOrDefault();
            //     if (firstNode?.Status?.NodeInfo != null)
            //     {
            //         osType = firstNode.Status.NodeInfo.OperatingSystem;
            //         arch = firstNode.Status.NodeInfo.Architecture;
            //         version = firstNode.Status.NodeInfo.OsImage;
            //     }
            //     else
            //     {
            //         Events.NullNodeInfoResponse(firstNode?.Metadata?.Name ?? "UNKNOWN");
            //     }
            // }
            string osType = "Kubernetes";
            string arch = "Kubernetes";
            string version = "Kubernetes";
            return Task.FromResult(new SystemInfo(osType, arch, version));
        }

        async Task WatchPodEventsAsync(WatchEventType type, V1Pod item)
        {
            // if the pod doesn't have the module label set then we are not interested in it
            if (!item.Metadata.Labels.TryGetValue(Constants.K8sEdgeModuleLabel, out string podName))
            {
                return;
            }

            Events.PodStatus(type, podName);
            switch (type)
            {
                case WatchEventType.Added:
                case WatchEventType.Modified:
                case WatchEventType.Error:
                    ModuleRuntimeInfo runtimeInfo = item.ConvertToRuntime(podName);
                    using (await this.moduleLock.LockAsync())
                    {
                        this.moduleRuntimeInfos[podName] = runtimeInfo;
                        this.OnModulesChanged();
                    }

                    break;
                case WatchEventType.Deleted:
                    using (await this.moduleLock.LockAsync())
                    {
                        if (!this.moduleRuntimeInfos.TryRemove(podName, out _))
                        {
                            Events.PodStatusRemoveError(podName);
                        }

                        this.OnModulesChanged();
                    }

                    break;
            }
        }

        // TODO consider to delete. Now it is used only for tests
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnModulesChanged()
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Modules"));
        }

        static class Events
        {
            const int IdStart = KubernetesEventIds.KubernetesRuntimeInfoProvider;
            private static readonly ILogger Log = Logger.Factory.CreateLogger<KubernetesRuntimeInfoProvider>();

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
}
