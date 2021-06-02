// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using k8s;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Edgelet;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment;
    using Microsoft.Azure.Devices.Edge.Util;
    using AgentDocker = Microsoft.Azure.Devices.Edge.Agent.Docker;
    public class KubernetesRuntimeInfoProvider : IRuntimeInfoProvider, IRuntimeInfoSource
    {
        readonly string deviceNamespace;
        readonly IKubernetes client;
        readonly ConcurrentDictionary<string, ImmutableList<KeyValuePair<string, ModuleRuntimeInfo>>> moduleRuntimeInfo;
        readonly IModuleManager moduleManager;

        public KubernetesRuntimeInfoProvider(string deviceNamespace, IKubernetes client, IModuleManager moduleManager)
        {
            this.deviceNamespace = Preconditions.CheckNonWhiteSpace(deviceNamespace, nameof(deviceNamespace));
            this.client = Preconditions.CheckNotNull(client, nameof(client));
            this.moduleManager = Preconditions.CheckNotNull(moduleManager, nameof(moduleManager));
            this.moduleRuntimeInfo = new ConcurrentDictionary<string, ImmutableList<KeyValuePair<string, ModuleRuntimeInfo>>>();
        }

        public void CreateOrUpdateAddPodInfo(V1Pod pod)
        {
            string moduleName = pod.Metadata.Labels[Constants.K8sEdgeModuleLabel];
            var newPair = new KeyValuePair<string, ModuleRuntimeInfo>(pod.Metadata.Name, pod.ConvertToRuntime(moduleName));

            this.moduleRuntimeInfo.AddOrUpdate(
                moduleName,
                ImmutableList.Create(newPair),
                (_, existing) =>
                {
                    return existing.FirstOption(pair => pair.Key == pod.Metadata.Name)
                        .Map(pair => existing.Replace(pair, newPair))
                        .GetOrElse(() => existing.Add(newPair));
                });
        }

        public bool RemovePodInfo(V1Pod pod)
        {
            var moduleName = pod.Metadata.Labels[Constants.K8sEdgeModuleLabel];
            if (this.moduleRuntimeInfo.TryGetValue(moduleName, out var existing))
            {
                return existing
                    .FirstOption(pair => pair.Key == pod.Metadata.Name)
                    .Map(
                        pair =>
                        {
                            ImmutableList<KeyValuePair<string, ModuleRuntimeInfo>> list = existing.Remove(pair);
                            return !list.IsEmpty
                                ? this.moduleRuntimeInfo.TryUpdate(moduleName, list, existing)
                                : this.moduleRuntimeInfo.Remove(moduleName, out _);
                        })
                    .OrDefault();
            }

            return false;
        }

        public async Task<IEnumerable<ModuleRuntimeInfo>> GetModules(CancellationToken cancellationToken)
        {
            IEnumerable<ModuleRuntimeInfo> moduleRuntimeInfoList = this.moduleRuntimeInfo.Values
                .Select(list => list.Last().Value)
                .ToList();
            return await Task.FromResult(moduleRuntimeInfoList);
        }

        public Task<Stream> GetModuleLogs(string module, bool follow, Option<int> tail, Option<string> since, Option<string> until, CancellationToken cancellationToken)
        {
            return Task.FromResult(Stream.Null);
        }

        public Task<SystemInfo> GetSystemInfo(CancellationToken cancellationToken) => this.moduleManager.GetSystemInfoAsync(cancellationToken);
    }
}
