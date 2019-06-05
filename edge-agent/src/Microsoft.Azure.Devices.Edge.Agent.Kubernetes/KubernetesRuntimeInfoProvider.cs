// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using k8s;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Rest;
    using Microsoft.Extensions.Logging;
    using AgentDocker = Microsoft.Azure.Devices.Edge.Agent.Docker;
    using System.IO;

    public class KubernetesRuntimeInfoProvider : IKubernetesOperator, IRuntimeInfoProvider
    {
        readonly IKubernetes client;
        Option<Watcher<V1Pod>> podWatch;
        readonly Dictionary<string, ModuleRuntimeInfo> moduleRuntimeInfos;
        readonly AsyncLock moduleLock;

        public KubernetesRuntimeInfoProvider(IKubernetes client)
        {
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
                    this.podWatch = Option.Some(podListResp.Watch<V1Pod>(
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
                                this.client.ListNamespacedPodWithHttpMessagesAsync(KubeUtils.K8sNamespace, watch: true).ContinueWith(this.ListPodComplete);
                            },
                            onError: Events.ExceptionInPodWatch
                        ));
                }
                else
                {
                    Events.NullListResponse("ListNamespacedPodWithHttpMessagesAsync", "http response");
                    throw new NullReferenceException("Null response from ListNamespacedPodWithHttpMessagesAsync");
                }
            }
            else
            {
                Events.NullListResponse("ListNamespacedPodWithHttpMessagesAsync", "task");
                throw new NullReferenceException("Null Task from ListNamespacedPodWithHttpMessagesAsync");
            }
        }


        public void Start() => this.client.ListNamespacedPodWithHttpMessagesAsync(KubeUtils.K8sNamespace, watch: true).ContinueWith(this.ListPodComplete);

        public Task CloseAsync(CancellationToken token) => Task.CompletedTask;

        public void Dispose() => this.podWatch.ForEach(watch => watch.Dispose());

        public async Task<IEnumerable<ModuleRuntimeInfo>> GetModules(CancellationToken cancellationToken)
        {
            using (await this.moduleLock.LockAsync(cancellationToken))
            {
                return this.moduleRuntimeInfos.Select(kvp => kvp.Value);
            }
        }

        public Task<Stream> GetModuleLogs(string module, bool follow, Option<int> tail, Option<int> since, CancellationToken cancellationToken) => Task.FromResult(Stream.Null);

        public async Task<SystemInfo> GetSystemInfo()
        {
            V1NodeList k8SNodes = await this.client.ListNodeAsync();
            string osType = string.Empty;
            string arch = string.Empty;
            string version = string.Empty;
            if (k8SNodes.Items != null)
            {
                V1Node firstNode = k8SNodes.Items.FirstOrDefault();

                if (firstNode?.Status?.NodeInfo != null)
                {
                    osType = firstNode.Status.NodeInfo.OperatingSystem;
                    arch = firstNode.Status.NodeInfo.Architecture;
                    version = firstNode.Status.NodeInfo.OsImage;
                }
                else
                {
                    Events.NullNodeInfoResponse(firstNode?.Metadata?.Name ?? "UNKNOWN");
                } 
            }
            return new SystemInfo(osType, arch, version);
        }

        async Task WatchPodEventsAsync(WatchEventType type, V1Pod item)
        {
            // if the pod doesn't have the module label set then we are not interested in it
            if (item.Metadata.Labels.ContainsKey(Constants.k8sEdgeModuleLabel) == false)
            {
                return;
            }

            string podName = item.Metadata.Labels[Constants.k8sEdgeModuleLabel];
            Events.PodStatus(type, podName);
            switch (type)
            {
                case WatchEventType.Added:
                case WatchEventType.Modified:
                case WatchEventType.Error:
                    ModuleRuntimeInfo runtimeInfo = this.ConvertPodToRuntime(podName, item);
                    using (await this.moduleLock.LockAsync())
                    {
                        this.moduleRuntimeInfos[podName] = runtimeInfo;
                    }
                    break;
                case WatchEventType.Deleted:
                    using (await this.moduleLock.LockAsync())
                    {
                        ModuleRuntimeInfo removedRuntimeInfo;
                        if (!this.moduleRuntimeInfos.TryRemove(podName, out removedRuntimeInfo))
                        {
                            Events.PodStatusRemoveError(podName);
                        }
                    }
                    break;

            }
        }

        (ModuleStatus, string) ConvertPodStatusToModuleStatus(Option<V1ContainerStatus> podStatus)
        {
            // TODO: Possibly refine this?
            return podStatus.Map(pod =>
            {
                if (pod.State != null)
                {
                    if (pod.State.Running != null)
                    {
                        return (ModuleStatus.Running, $"Started at {pod.State.Running.StartedAt.GetValueOrDefault(DateTime.Now)}");
                    }
                    if (pod.State.Terminated != null)
                    {
                        return (ModuleStatus.Failed, pod.State.Terminated.Message);
                    }
                    if (pod.State.Waiting != null)
                    {
                        return (ModuleStatus.Failed, pod.State.Waiting.Message);
                    }
                }
                return (ModuleStatus.Unknown, "Unknown");
            }).GetOrElse(() => (ModuleStatus.Unknown, "Unknown"));
        }

        static Option<V1ContainerStatus> GetContainerByName(string name, V1Pod pod)
        {
            string containerName = KubeUtils.SanitizeDNSValue(name);
            if (pod.Status?.ContainerStatuses != null)
            {
                foreach (var status in pod.Status?.ContainerStatuses)
                {
                    if (string.Equals(status.Name, containerName, StringComparison.OrdinalIgnoreCase))
                    {
                        return Option.Some(status);
                    }
                }
            }

            return Option.None<V1ContainerStatus>();
        }

        static (int, Option<DateTime>, Option<DateTime>, string image) GetRuntimedata(V1ContainerStatus status)
        {
            if (status?.LastState?.Running != null)
            {
                if (status.LastState.Running.StartedAt.HasValue)
                {
                    return (0, Option.Some(status.LastState.Running.StartedAt.Value), Option.None<DateTime>(), status.Image);
                }
            }
            else if (status?.LastState?.Terminated != null)
            {
                if (status.LastState.Terminated.StartedAt.HasValue &&
                    status.LastState.Terminated.FinishedAt.HasValue)
                {
                    return (0, Option.Some(status.LastState.Terminated.StartedAt.Value), Option.Some(status.LastState.Terminated.FinishedAt.Value), status.Image);
                }
            }
            return (0, Option.None<DateTime>(), Option.None<DateTime>(), string.Empty);
        }

        ModuleRuntimeInfo ConvertPodToRuntime(string name, V1Pod pod)
        {
            Option<V1ContainerStatus> containerStatus = GetContainerByName(name, pod);
            (ModuleStatus moduleStatus, string statusDescription) = this.ConvertPodStatusToModuleStatus(containerStatus);
            (int exitCode, Option<DateTime> startTime, Option<DateTime> exitTime, string image) = GetRuntimedata(containerStatus.OrDefault());

            string moduleName = name;
            pod.Metadata?.Annotations?.TryGetValue(Constants.k8sEdgeOriginalModuleId, out moduleName);

            var reportedConfig = new AgentDocker.DockerReportedConfig(image, string.Empty, string.Empty);
            return new ModuleRuntimeInfo<AgentDocker.DockerReportedConfig>(
                ModuleIdentityHelper.GetModuleName(moduleName), "docker", moduleStatus, statusDescription, exitCode,
                startTime, exitTime, reportedConfig);
        }


        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<KubernetesRuntimeInfoProvider>();
            const int IdStart = AgentEventIds.KubernetesReporter;

            enum EventIds
            {
                ExceptionInPodWatch = IdStart,
                PodStatus,
                PodStatusRemoveError,
                NullListResponse,
                NullNodeInfoResponse,
                PodWatchClosed,
                CrdWatchClosed,
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

            public static void CrdWatchClosed()
            {
                Log.LogInformation((int)EventIds.CrdWatchClosed, $"K8s closed the CRD watch. Attempting to reopen watch.");
            }
        }
    }
}
