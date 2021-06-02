// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Docker.Models;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    /// <summary>
    /// This implementation gets the module runtime information from IRuntimeInfoProvider and
    /// the configuration information from the deploymentConfig.
    /// TODO: This could be made generic (not docker specific) and moved to Core.
    /// </summary>
    public class DockerEnvironment : IEnvironment
    {
        readonly IRuntimeInfoProvider moduleStatusProvider;
        readonly IEntityStore<string, ModuleState> moduleStateStore;
        readonly string operatingSystemType;
        readonly string architecture;
        readonly string version;
        readonly DeploymentConfig deploymentConfig;
        readonly IRestartPolicyManager restartManager;

        public DockerEnvironment(
            IRuntimeInfoProvider moduleStatusProvider,
            DeploymentConfig deploymentConfig,
            IEntityStore<string, ModuleState> moduleStateStore,
            IRestartPolicyManager restartManager,
            string operatingSystemType,
            string architecture,
            string version)
        {
            this.moduleStatusProvider = moduleStatusProvider;
            this.deploymentConfig = deploymentConfig;
            this.moduleStateStore = moduleStateStore;
            this.restartManager = restartManager;
            this.operatingSystemType = operatingSystemType;
            this.architecture = architecture;
            this.version = version;
        }

        public async Task<ModuleSet> GetModulesAsync(CancellationToken token)
        {
            IEnumerable<ModuleRuntimeInfo> moduleStatuses = await this.moduleStatusProvider.GetModules(token);
            var modules = new List<IModule>();
            string image;
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
                    // This is given the highest priority so that it's removal is prioritized first before other known modules are processed.
                    dockerModule = new DockerModule(dockerRuntimeInfo.Name, string.Empty, ModuleStatus.Unknown, Core.RestartPolicy.Unknown, new DockerConfig(Constants.UnknownImage, new CreateContainerParameters(), Option.None<string>()), ImagePullPolicy.OnCreate, Core.Constants.HighestPriority, new ConfigurationInfo(), null);
                }

                Option<ModuleState> moduleStateOption = await this.moduleStateStore.Get(moduleRuntimeInfo.Name);
                ModuleState moduleState = moduleStateOption.GetOrElse(new ModuleState(0, moduleRuntimeInfo.ExitTime.GetOrElse(DateTime.MinValue)));
                // compute module state based on restart policy
                DateTime lastExitTime = moduleRuntimeInfo.ExitTime.GetOrElse(DateTime.MinValue);
                ModuleStatus moduleRuntimeStatus = dockerModule.DesiredStatus == ModuleStatus.Running
                    ? this.restartManager.ComputeModuleStatusFromRestartPolicy(moduleRuntimeInfo.ModuleStatus, dockerModule.RestartPolicy, moduleState.RestartCount, lastExitTime)
                    : moduleRuntimeInfo.ModuleStatus;

                // Check to see if the originalImage label is set by the iotedege daemon. This label is set for content trust feature where the image is pulled by digest.
                // OriginalImageLabelStr contains the image with tag and it is used to handle the mismatch of image with digest during reconcilation.
                var labels = dockerRuntimeInfo.Config.CreateOptions?.Labels ?? new Dictionary<string, string>();
                labels.TryGetValue(Core.Constants.Labels.OriginalImage, out string originalImageLabelStr);
                if (!string.IsNullOrWhiteSpace(originalImageLabelStr))
                {
                    image = originalImageLabelStr;
                }
                else
                {
                    image = !string.IsNullOrWhiteSpace(dockerRuntimeInfo.Config.Image) ? dockerRuntimeInfo.Config.Image : dockerModule.Config.Image;
                }

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
                            dockerRuntimeInfo.Description,
                            dockerRuntimeInfo.StartTime.GetOrElse(DateTime.MinValue),
                            lastExitTime,
                            moduleState.RestartCount,
                            moduleState.LastRestartTimeUtc,
                            moduleRuntimeStatus,
                            dockerModule.ImagePullPolicy,
                            dockerModule.StartupOrder,
                            dockerModule.ConfigurationInfo,
                            dockerModule.Env,
                            dockerModule.Version);
                        break;

                    case Core.Constants.EdgeAgentModuleName:
                        var env = labels.TryGetValue(Core.Constants.Labels.Env, out string envStr)
                            ? JsonConvert.DeserializeObject<Dictionary<string, EnvVal>>(envStr)
                            : new Dictionary<string, EnvVal>();
                        dockerReportedConfig = new DockerReportedConfig(image, dockerRuntimeInfo.Config.CreateOptions, dockerRuntimeInfo.Config.ImageHash, dockerRuntimeInfo.Config.Digest);

                        module = new EdgeAgentDockerRuntimeModule(
                            dockerReportedConfig,
                            moduleRuntimeStatus,
                            (int)dockerRuntimeInfo.ExitCode,
                            dockerRuntimeInfo.Description,
                            dockerRuntimeInfo.StartTime.GetOrElse(DateTime.MinValue),
                            lastExitTime,
                            dockerModule.ImagePullPolicy,
                            dockerModule.ConfigurationInfo,
                            env,
                            dockerModule.Version);
                        break;

                    default:
                        module = new DockerRuntimeModule(
                            moduleRuntimeInfo.Name,
                            dockerModule.Version,
                            dockerModule.DesiredStatus,
                            dockerModule.RestartPolicy,
                            dockerReportedConfig,
                            (int)moduleRuntimeInfo.ExitCode,
                            moduleRuntimeInfo.Description,
                            moduleRuntimeInfo.StartTime.GetOrElse(DateTime.MinValue),
                            lastExitTime,
                            moduleState.RestartCount,
                            moduleState.LastRestartTimeUtc,
                            moduleRuntimeStatus,
                            dockerModule.ImagePullPolicy,
                            dockerModule.StartupOrder,
                            dockerModule.ConfigurationInfo,
                            dockerModule.Env);
                        break;
                }

                modules.Add(module);
            }

            return new ModuleSet(modules.ToDictionary(m => m.Name, m => m));
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
            static readonly ILogger Log = Logger.Factory.CreateLogger<DockerEnvironment>();

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
