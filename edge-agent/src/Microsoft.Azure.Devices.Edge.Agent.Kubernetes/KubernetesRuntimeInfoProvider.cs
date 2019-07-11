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
        private readonly KubernetesEventLogger<KubernetesRuntimeInfoProvider> logger = new KubernetesEventLogger<KubernetesRuntimeInfoProvider>();

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
                                    this.logger.ExceptionInPodWatch(ex);
                                }
                            },
                            onClosed: () =>
                            {
                                this.logger.PodWatchClosed();

                                // get rid of the current pod watch object since we got closed
                                this.podWatch.ForEach(watch => watch.Dispose());
                                this.podWatch = Option.None<Watcher<V1Pod>>();

                                // kick off a new watch
                                this.client.ListNamespacedPodWithHttpMessagesAsync(this.deviceNamespace, watch: true).ContinueWith(this.ListPodComplete);
                            },
                            onError: this.logger.ExceptionInPodWatch));
                }
                else
                {
                    this.logger.NullListResponse("ListNamespacedPodWithHttpMessagesAsync", "http response");
                    throw new KuberenetesResponseException("Null response from ListNamespacedPodWithHttpMessagesAsync");
                }
            }
            else
            {
                this.logger.NullListResponse("ListNamespacedPodWithHttpMessagesAsync", "task");
                throw new KuberenetesResponseException("Null Task from ListNamespacedPodWithHttpMessagesAsync");
            }
        }

        public void Start() => this.client.ListNamespacedPodWithHttpMessagesAsync(this.deviceNamespace, watch: true).ContinueWith(this.ListPodComplete);

        public Task CloseAsync(CancellationToken token) => Task.CompletedTask;

        public void Dispose() => this.podWatch.ForEach(watch => watch.Dispose());

        public async Task<IEnumerable<ModuleRuntimeInfo>> GetModules(CancellationToken cancellationToken)
        {
            using (await this.moduleLock.LockAsync(cancellationToken))
            {
                return this.moduleRuntimeInfos.Select(kvp => kvp.Value);
            }
        }

        public async Task<Stream> GetModuleLogs(string module, bool follow, Option<int> tail, Option<int> since, CancellationToken cancellationToken)
        {
            return await this.client.ReadNamespacedPodLogAsync(
                module,
                Constants.K8sNamespace,
                follow: follow,
                tailLines: tail.GetOrElse(null),
                sinceSeconds: since.GetOrElse(null),
                cancellationToken: cancellationToken);
        }

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
                    this.logger.NullNodeInfoResponse(firstNode?.Metadata?.Name ?? "UNKNOWN");
                }
            }

            return new SystemInfo(osType, arch, version);
        }

        async Task WatchPodEventsAsync(WatchEventType type, V1Pod item)
        {
            // if the pod doesn't have the module label set then we are not interested in it
            if (!item.Metadata.Labels.TryGetValue(Constants.K8sEdgeModuleLabel, out string podName))
            {
                return;
            }

            this.logger.PodStatus(type, podName);
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
                        ModuleRuntimeInfo removedRuntimeInfo;
                        if (!this.moduleRuntimeInfos.TryRemove(podName, out removedRuntimeInfo))
                        {
                            this.logger.PodStatusRemoveError(podName);
                        }

                        this.OnModulesChanged();
                    }

                    break;
            }
        }

        static RuntimeData GetTerminatedRuntimedata(V1ContainerStateTerminated term, string imageName)
        {
            if (term.StartedAt.HasValue &&
                term.FinishedAt.HasValue)
            {
                return new RuntimeData(term.ExitCode, Option.Some(term.StartedAt.Value), Option.Some(term.FinishedAt.Value), imageName);
            }

            return new RuntimeData(0, Option.None<DateTime>(), Option.None<DateTime>(), imageName);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnModulesChanged()
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Modules"));
        }

        class ReportedModuleStatus
        {
            public readonly ModuleStatus Status;
            public readonly string Description;

            public ReportedModuleStatus(ModuleStatus status, string description)
            {
                this.Status = status;
                this.Description = description;
            }
        }

        class RuntimeData
        {
            public readonly int ExitStatus;
            public readonly Option<DateTime> StartTime;
            public readonly Option<DateTime> EndTime;
            public readonly string ImageName;

            public RuntimeData(int exitStatus, Option<DateTime> startTime, Option<DateTime> endTime, string image)
            {
                this.ExitStatus = exitStatus;
                this.StartTime = startTime;
                this.EndTime = endTime;
                this.ImageName = image;
            }
        }
    }
}
