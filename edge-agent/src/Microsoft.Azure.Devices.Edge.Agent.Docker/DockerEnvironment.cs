// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Docker
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Docker.DotNet;
    using global::Docker.DotNet.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using CoreConstants = Core.Constants;

    public class DockerEnvironment : IEnvironment
    {
        static readonly IDictionary<string, bool> Labels = new Dictionary<string, bool>
        {
            { $"{CoreConstants.Labels.Owner}={CoreConstants.Owner}", true }
        };

        readonly IDockerClient client;
        readonly IEntityStore<string, ModuleState> store;
        readonly IRestartPolicyManager restartManager;

        DockerEnvironment(IDockerClient client, IEntityStore<string, ModuleState> store, IRestartPolicyManager restartManager, string operatingSystemType, string architecture)
        {
            this.client = Preconditions.CheckNotNull(client, nameof(client));
            this.store = Preconditions.CheckNotNull(store, nameof(store));
            this.restartManager = Preconditions.CheckNotNull(restartManager, nameof(restartManager));

            this.OperatingSystemType = string.IsNullOrWhiteSpace(operatingSystemType) ? "Unknown" : operatingSystemType;
            this.Architecture = string.IsNullOrWhiteSpace(architecture) ? "Unknown" : architecture;
        }

        public string OperatingSystemType { get; }

        public string Architecture { get; }

        public async static Task<DockerEnvironment> CreateAsync(IDockerClient client, IEntityStore<string, ModuleState> store, IRestartPolicyManager restartManager)
        {
            Preconditions.CheckNotNull(client, nameof(client));

            // get system information from docker
            SystemInfoResponse info = await client.System.GetSystemInfoAsync();

            return new DockerEnvironment(client, store, restartManager, info.OSType, info.Architecture);
        }

        public Task<IRuntimeInfo> GetUpdatedRuntimeInfoAsync(IRuntimeInfo runtimeInfo)
        {
            Preconditions.CheckArgument(string.Equals(runtimeInfo?.Type, "docker"));

            string type = runtimeInfo?.Type;
            DockerRuntimeConfig config = (runtimeInfo as DockerRuntimeInfo)?.Config;
            var platform = new DockerPlatformInfo(this.OperatingSystemType, this.Architecture);

            return Task.FromResult(new DockerReportedRuntimeInfo(type, config, platform) as IRuntimeInfo);
        }

        public async Task<ModuleSet> GetModulesAsync(CancellationToken token)
        {
            var parameters = new ContainersListParameters
            {
                All = true,
                Filters = new Dictionary<string, IDictionary<string, bool>>
                {
                    { "label", Labels }
                }
            };
            IList<ContainerListResponse> containers = await this.client.Containers.ListContainersAsync(parameters);
            IModule[] modules = await Task.WhenAll(containers.Select(c => this.ContainerToModuleAsync(c)));
            return new ModuleSet(modules.ToDictionary(m => m.Name, m => m));
        }

        async Task<Option<ContainerInspectResponse>> GetEdgeAgentContainerAsync(CancellationToken token)
        {
            try
            {
                ContainerInspectResponse response = await this.client.Containers.InspectContainerAsync(CoreConstants.EdgeAgentModuleName, token);
                return Option.Some(response);
            }
            catch (DockerContainerNotFoundException ex)
            {
                Events.EdgeAgentContainerNotFound(ex);
                return Option.None<ContainerInspectResponse>();
            }
        }

        public async Task<IEdgeAgentModule> GetEdgeAgentModuleAsync(CancellationToken token)
        {
            Option<ContainerInspectResponse> edgeAgentContainer = await this.GetEdgeAgentContainerAsync(token);

            // TODO: We have more information that we could report about edge agent here. For example
            // we could serialize the entire edgeAgentContainer response object and report it.
            DockerConfig config = edgeAgentContainer
                .Map(response => new DockerConfig(response.Image, Environment.GetEnvironmentVariable(Constants.EdgeAgentCreateOptionsName)))
                .GetOrElse(DockerConfig.Unknown);

            // TODO: When we have health checks for Edge Agent the runtime status can potentially be "Unhealthy".
            return new EdgeAgentDockerRuntimeModule(config, ModuleStatus.Running, new ConfigurationInfo());
        }

        (
            string name,
            string version,
            string image,
            ModuleStatus desiredStatus,
            Core.RestartPolicy RestartPolicy,
            DockerConfig dockerConfig,
            ConfigurationInfo configurationInfo
        )
        ExtractModuleInfo(ContainerListResponse response)
        {
            string name = response.Names.FirstOrDefault()?.Substring(1) ?? "unknown";
            string version = response.Labels.GetOrElse(CoreConstants.Labels.Version, string.Empty);
            Core.RestartPolicy restartPolicy = (Core.RestartPolicy)Enum.Parse(
                typeof(Core.RestartPolicy),
                response.Labels.GetOrElse(
                    CoreConstants.Labels.RestartPolicy,
                    CoreConstants.DefaultRestartPolicy.ToString()
                )
            );
            ModuleStatus desiredStatus = (ModuleStatus)Enum.Parse(
                typeof(ModuleStatus),
                response.Labels.GetOrElse(
                    CoreConstants.Labels.DesiredStatus,
                    CoreConstants.DefaultDesiredStatus.ToString()
                )
            );
            string image = response.Image != null ? response.Image : "unknown";
            var dockerConfig = new DockerConfig(image, (response.Labels.GetOrElse(CoreConstants.Labels.NormalizedCreateOptions, string.Empty)));
            var configurationInfo = new ConfigurationInfo(response.Labels.GetOrElse(CoreConstants.Labels.ConfigurationId, string.Empty));

            return (name, version, image, desiredStatus, restartPolicy, dockerConfig, configurationInfo);
        }

        (
            int exitCode,
            string statusDescription,
            DateTime lastStartTime,
            DateTime lastExitTime
        )
        ExtractModuleRuntimeState(ContainerInspectResponse inspected)
        {
            int exitCode = (inspected?.State != null) ? (int)inspected.State.ExitCode : 0;
            string statusDescription = inspected?.State?.Status;

            string lastStartTimeStr = inspected?.State?.StartedAt;
            DateTime lastStartTime = DateTime.MinValue;
            if (lastStartTimeStr != null)
            {
                lastStartTime = DateTime.Parse(lastStartTimeStr, null, DateTimeStyles.RoundtripKind);
            }

            string lastExitTimeStr = inspected?.State?.FinishedAt;
            DateTime lastExitTime = DateTime.MinValue;
            if (lastExitTime != null)
            {
                lastExitTime = DateTime.Parse(lastExitTimeStr, null, DateTimeStyles.RoundtripKind);
            }

            return (exitCode, statusDescription, lastStartTime, lastExitTime);
        }

        internal async Task<IModule> ContainerToModuleAsync(ContainerListResponse response)
        {
            // Extract the following attributes from the container response object:
            //  - name
            //  - version
            //  - image
            //  - desired status
            //  - restart policy,
            //  - docker configuration
            //  - configuration info
            var (name, version, image, desiredStatus, restartPolicy, dockerConfig, configurationInfo) = this.ExtractModuleInfo(response);

            // Do a deep inspection of the container to get the following runtime state:
            //  - exit code
            //  - exit status description
            //  - last start time
            //  - lat exit time
            ContainerInspectResponse inspected = await this.client.Containers.InspectContainerAsync(response.ID);
            var (exitCode, statusDescription, lastStartTime, lastExitTime) = this.ExtractModuleRuntimeState(inspected);

            // Figure out module stats and runtime status
            ModuleState moduleState = (await this.store.Get(name)).GetOrElse(new ModuleState(0, lastExitTime));
            ModuleStatus runtimeStatus = ToRuntimeStatus(inspected.State, restartPolicy, moduleState.RestartCount, lastExitTime);

            if (name == CoreConstants.EdgeHubModuleName)
            {
                return new EdgeHubDockerRuntimeModule(
                    name, version, desiredStatus, restartPolicy, dockerConfig,
                    exitCode, statusDescription, lastStartTime, lastExitTime,
                    moduleState.RestartCount, moduleState.LastRestartTimeUtc,
                    runtimeStatus, configurationInfo
                );
            }
            else
            {
                return new DockerRuntimeModule(
                    name, version, desiredStatus, restartPolicy, dockerConfig,
                    exitCode, statusDescription, lastStartTime, lastExitTime,
                    moduleState.RestartCount, moduleState.LastRestartTimeUtc,
                    runtimeStatus, configurationInfo
                );
            }
        }

        ModuleStatus ToRuntimeStatus(ContainerState containerState, Core.RestartPolicy restartPolicy, int restartCount, DateTime lastExitTime)
        {
            ModuleStatus status;

            switch (containerState.Status.ToLower())
            {
                case "created":
                case "paused":
                case "restarting":
                    status = ModuleStatus.Stopped;
                    break;

                case "removing":
                case "dead":
                case "exited":
                    // if the exit code is anything other than zero then the container is
                    // considered as having "failed"; otherwise it is considered as stopped
                    status = containerState.ExitCode == 0 ? ModuleStatus.Stopped : ModuleStatus.Failed;
                    break;

                case "running":
                    status = ModuleStatus.Running;
                    break;

                default:
                    // TODO: What exactly does this state mean? Maybe we should just throw?
                    Events.InvalidContainerStatusFound(containerState.Status);
                    status = ModuleStatus.Unknown;
                    break;
            }

            // compute module state based on restart policy
            status = this.restartManager.ComputeModuleStatusFromRestartPolicy(
                status, restartPolicy, restartCount, lastExitTime
            );

            return status;
        }
    }

    static class Events
    {
        static readonly ILogger Log = Util.Logger.Factory.CreateLogger<DockerEnvironment>();
        const int IdStart = AgentEventIds.DockerEnvironment;

        enum EventIds
        {
            InvalidContainerStatus = IdStart,
            EdgeAgentContainerNotFound = IdStart + 1
        }

        public static void InvalidContainerStatusFound(string status)
        {
            Log.LogInformation((int)EventIds.InvalidContainerStatus, $"Encountered an unrecognized container state from Docker - {status}");
        }

        static bool EdgeAgentContainerNotFoundReported = false;

        public static void EdgeAgentContainerNotFound(DockerContainerNotFoundException ex)
        {
            if (EdgeAgentContainerNotFoundReported == false)
            {
                Log.LogWarning((int)EventIds.EdgeAgentContainerNotFound, $"No container for edge agent was found with the name {CoreConstants.EdgeAgentModuleName} - {ex.Message}");
                EdgeAgentContainerNotFoundReported = true;
            }
        }
    }
}
