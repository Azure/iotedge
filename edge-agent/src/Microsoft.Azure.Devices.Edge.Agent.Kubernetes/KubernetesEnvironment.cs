// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Microsoft.Azure.Devices.Edge.Agent.Docker.Models;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// This implementation gets the module runtime information from KubernetesRuntimeInfoProvider and
    /// the configuration information from the deploymentConfig.
    /// TODO: This could be made generic (not docker specific) and moved to Core.
    /// </summary>
    public class KubernetesEnvironment : IEnvironment
    {
        readonly IRuntimeInfoProvider moduleStatusProvider;
        readonly IEntityStore<string, ModuleState> moduleStateStore;
        readonly string operatingSystemType;
        readonly string architecture;
        readonly string version;
        readonly DeploymentConfig deploymentConfig;

        public KubernetesEnvironment(
            IRuntimeInfoProvider moduleStatusProvider,
            DeploymentConfig deploymentConfig,
            IEntityStore<string, ModuleState> moduleStateStore,
            string operatingSystemType,
            string architecture,
            string version)
        {
            this.moduleStatusProvider = moduleStatusProvider;
            this.deploymentConfig = deploymentConfig;
            this.moduleStateStore = moduleStateStore;
            this.operatingSystemType = operatingSystemType;
            this.architecture = architecture;
            this.version = version;
        }

        public async Task<ModuleSet> GetModulesAsync(CancellationToken token)
        {
            IEnumerable<ModuleRuntimeInfo> moduleStatuses = await this.moduleStatusProvider.GetModules(token);
            var modules = new List<IModule>();
            ModuleSet moduleSet = this.deploymentConfig.GetModuleSet();

            foreach (ModuleRuntimeInfo moduleRuntimeInfo in moduleStatuses)
            {
                if (moduleRuntimeInfo.Type != "docker" || !(moduleRuntimeInfo is ModuleRuntimeInfo<DockerReportedConfig> dockerRuntimeInfo))
                {
                    Events.InvalidModuleType(moduleRuntimeInfo);
                    continue;
                }

                if (!moduleSet.Modules.TryGetValue(dockerRuntimeInfo.Name, out IModule configModule) || !(configModule is DockerModule dockerModule))
                {
                    dockerModule = new DockerModule(dockerRuntimeInfo.Name, string.Empty, ModuleStatus.Unknown, Core.RestartPolicy.Unknown, new DockerConfig(Constants.UnknownImage, new CreateContainerParameters(), Option.None<string>()), ImagePullPolicy.OnCreate, Core.Constants.HighestPriority, new ConfigurationInfo(), null);
                }

                Option<ModuleState> moduleStateOption = await this.moduleStateStore.Get(moduleRuntimeInfo.Name);
                ModuleState moduleState = moduleStateOption.GetOrElse(new ModuleState(0, moduleRuntimeInfo.ExitTime.GetOrElse(DateTime.MinValue)));

                string image = !string.IsNullOrWhiteSpace(dockerRuntimeInfo.Config.Image) ? dockerRuntimeInfo.Config.Image : dockerModule.Config.Image;
                var dockerReportedConfig = new DockerReportedConfig(image, dockerModule.Config.CreateOptions, dockerRuntimeInfo.Config.ImageHash, dockerModule.Config.Digest);
                IModule module;
                switch (moduleRuntimeInfo.Name)
                {
                    case Core.Constants.EdgeHubModuleName:
                        module = new EdgeHubDockerRuntimeModule(
                            dockerModule.DesiredStatus,
                            dockerModule.RestartPolicy,
                            dockerReportedConfig,
                            (int)dockerRuntimeInfo.ExitCode,
                            moduleRuntimeInfo.Description,
                            moduleRuntimeInfo.StartTime.GetOrElse(DateTime.MinValue),
                            moduleRuntimeInfo.ExitTime.GetOrElse(DateTime.MinValue),
                            moduleState.RestartCount,
                            moduleState.LastRestartTimeUtc,
                            moduleRuntimeInfo.ModuleStatus,
                            dockerModule.ImagePullPolicy,
                            dockerModule.StartupOrder,
                            dockerModule.ConfigurationInfo,
                            dockerModule.Env);
                        break;

                    case Core.Constants.EdgeAgentModuleName:
                        module = new EdgeAgentDockerRuntimeModule(
                            dockerReportedConfig,
                            moduleRuntimeInfo.ModuleStatus,
                            (int)dockerRuntimeInfo.ExitCode,
                            moduleRuntimeInfo.Description,
                            moduleRuntimeInfo.StartTime.GetOrElse(DateTime.MinValue),
                            moduleRuntimeInfo.ExitTime.GetOrElse(DateTime.MinValue),
                            dockerModule.ImagePullPolicy,
                            dockerModule.ConfigurationInfo,
                            dockerModule.Env);
                        break;

                    default:
                        module = new DockerRuntimeModule(
                            moduleRuntimeInfo.Name,
                            dockerModule.Version,
                            dockerModule.DesiredStatus,
                            dockerModule.RestartPolicy,
                            dockerReportedConfig,
                            (int)dockerRuntimeInfo.ExitCode,
                            moduleRuntimeInfo.Description,
                            moduleRuntimeInfo.StartTime.GetOrElse(DateTime.MinValue),
                            moduleRuntimeInfo.ExitTime.GetOrElse(DateTime.MinValue),
                            moduleState.RestartCount,
                            moduleState.LastRestartTimeUtc,
                            moduleRuntimeInfo.ModuleStatus,
                            dockerModule.ImagePullPolicy,
                            dockerModule.StartupOrder,
                            dockerModule.ConfigurationInfo,
                            dockerModule.Env);
                        break;
                }

                modules.Add(module);
            }

            return ModuleSet.Create(modules.ToArray());
        }

        public Task<IRuntimeInfo> GetRuntimeInfoAsync()
        {
            IRuntimeInfo runtimeInfo = this.deploymentConfig.Runtime;
            if (runtimeInfo?.Type == "docker")
            {
                var platform = new DockerPlatformInfo(this.operatingSystemType, this.architecture, this.version);
                DockerRuntimeConfig config = (runtimeInfo as DockerRuntimeInfo)?.Config;
                runtimeInfo = new DockerReportedRuntimeInfo(runtimeInfo.Type, config, platform);
            }
            else if (runtimeInfo == null || runtimeInfo is UnknownRuntimeInfo)
            {
                var platform = new DockerPlatformInfo(this.operatingSystemType, this.architecture, this.version);
                runtimeInfo = new DockerReportedUnknownRuntimeInfo(platform);
            }

            return Task.FromResult(runtimeInfo);
        }

        static class Events
        {
            const int IdStart = AgentEventIds.DockerEnvironment;
            static readonly ILogger Log = Logger.Factory.CreateLogger<KubernetesEnvironment>();

            enum EventIds
            {
                InvalidModuleType = IdStart
            }

            public static void InvalidModuleType(ModuleRuntimeInfo moduleRuntimeInfo)
            {
                Log.LogWarning((int)EventIds.InvalidModuleType, $"Module {moduleRuntimeInfo.Name} has an invalid module type '{moduleRuntimeInfo.Type}'. Expected type 'docker'");
            }
        }
    }
}
