// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Docker
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;

    public class DockerEnvironmentProvider : IEnvironmentProvider
    {
        readonly IRuntimeInfoProvider moduleStatusProvider;
        readonly IEntityStore<string, ModuleState> store;
        readonly IRestartPolicyManager restartPolicyManager;
        readonly string operatingSystemType;
        readonly string architecture;
        readonly string version;

        DockerEnvironmentProvider(
            IRuntimeInfoProvider runtimeInfoProvider,
            IEntityStore<string, ModuleState> store,
            IRestartPolicyManager restartPolicyManager,
            string operatingSystemType,
            string architecture,
            string version)
        {
            this.moduleStatusProvider = runtimeInfoProvider;
            this.store = Preconditions.CheckNotNull(store, nameof(store));
            this.operatingSystemType = operatingSystemType;
            this.architecture = architecture;
            this.version = version;
            this.restartPolicyManager = Preconditions.CheckNotNull(restartPolicyManager, nameof(restartPolicyManager));
        }

        public static async Task<DockerEnvironmentProvider> CreateAsync(IRuntimeInfoProvider runtimeInfoProvider, IEntityStore<string, ModuleState> store,
            IRestartPolicyManager restartPolicyManager)
        {
            SystemInfo systemInfo = await Preconditions.CheckNotNull(runtimeInfoProvider, nameof(runtimeInfoProvider)).GetSystemInfo();
            return new DockerEnvironmentProvider(
                runtimeInfoProvider, store, restartPolicyManager,
                systemInfo.OperatingSystemType, systemInfo.Architecture,
                systemInfo.Version);
        }

        public IEnvironment Create(DeploymentConfig deploymentConfig) =>
            new DockerEnvironment(
                this.moduleStatusProvider, deploymentConfig, this.store,
                this.restartPolicyManager, this.operatingSystemType,
                this.architecture, this.version);
    }
}
