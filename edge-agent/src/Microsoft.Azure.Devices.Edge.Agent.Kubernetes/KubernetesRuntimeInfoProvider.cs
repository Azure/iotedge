// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;
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
        readonly ConcurrentDictionary<string, ModuleRuntimeInfo> moduleRuntimeInfo;
        readonly IModuleManager moduleManager;

        public KubernetesRuntimeInfoProvider(string deviceNamespace, IKubernetes client, IModuleManager moduleManager)
        {
            this.deviceNamespace = Preconditions.CheckNonWhiteSpace(deviceNamespace, nameof(deviceNamespace));
            this.client = Preconditions.CheckNotNull(client, nameof(client));
            this.moduleManager = Preconditions.CheckNotNull(moduleManager, nameof(moduleManager));
            this.moduleRuntimeInfo = new ConcurrentDictionary<string, ModuleRuntimeInfo>();
        }

        public void CreateOrUpdateAddPodInfo(string podName, V1Pod pod)
        {
            ModuleRuntimeInfo runtimeInfo = pod.ConvertToRuntime(podName);
            this.moduleRuntimeInfo.AddOrUpdate(podName, runtimeInfo, (_, existing) => runtimeInfo);
        }

        public bool RemovePodInfo(string podName)
        {
            return this.moduleRuntimeInfo.TryRemove(podName, out _);
        }

        public async Task<IEnumerable<ModuleRuntimeInfo>> GetModules(CancellationToken cancellationToken) =>
            await Task.FromResult(this.moduleRuntimeInfo.Select(kvp => kvp.Value).ToList());

        public async Task<Stream> GetModuleLogs(string module, bool follow, Option<int> tail, Option<int> since, CancellationToken cancellationToken) =>
            await this.client.ReadNamespacedPodLogAsync(
                module,
                this.deviceNamespace,
                follow: follow,
                tailLines: tail.Map(lines => (int?)lines).OrDefault(),
                sinceSeconds: since.Map(sec => (int?)sec).OrDefault(),
                cancellationToken: cancellationToken);

        public Task<SystemInfo> GetSystemInfo()
        {
            return this.moduleManager.GetSystemInfoAsync();
        }
    }
}
