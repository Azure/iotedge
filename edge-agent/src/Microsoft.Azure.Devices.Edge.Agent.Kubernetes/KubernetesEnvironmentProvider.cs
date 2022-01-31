// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;

    public class KubernetesEnvironmentProvider : IEnvironmentProvider
    {
        readonly IRuntimeInfoProvider moduleStatusProvider;
        readonly IEntityStore<string, ModuleState> store;
        readonly string operatingSystemType;
        readonly string architecture;
        readonly string version;

        KubernetesEnvironmentProvider(
            IRuntimeInfoProvider runtimeInfoProvider,
            IEntityStore<string, ModuleState> store,
            string operatingSystemType,
            string architecture,
            string version)
        {
            this.moduleStatusProvider = runtimeInfoProvider;
            this.store = Preconditions.CheckNotNull(store, nameof(store));
            this.operatingSystemType = operatingSystemType;
            this.architecture = architecture;
            this.version = version;
        }

        public static async Task<KubernetesEnvironmentProvider> CreateAsync(
            IRuntimeInfoProvider runtimeInfoProvider,
            IEntityStore<string, ModuleState> store,
            CancellationToken token)
        {
            SystemInfo systemInfo = await Preconditions.CheckNotNull(runtimeInfoProvider, nameof(runtimeInfoProvider)).GetSystemInfo(token);
            return new KubernetesEnvironmentProvider(
                runtimeInfoProvider,
                store,
                systemInfo.OperatingSystemType,
                systemInfo.Architecture,
                systemInfo.Version);
        }

        public IEnvironment Create(DeploymentConfig deploymentConfig) =>
            new KubernetesEnvironment(
                this.moduleStatusProvider,
                deploymentConfig,
                this.store,
                this.operatingSystemType,
                this.architecture,
                this.version);
    }
}
