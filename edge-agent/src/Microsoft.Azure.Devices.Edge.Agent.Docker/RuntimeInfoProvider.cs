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
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using CoreConstants = Core.Constants;

    public class RuntimeInfoProvider : IRuntimeInfoProvider
    {
        static readonly IDictionary<string, bool> Labels = new Dictionary<string, bool>
        {
            { $"{CoreConstants.Labels.Owner}={CoreConstants.OwnerValue}", true }
        };

        readonly IDockerClient client;
        readonly string operatingSystemType;
        readonly string architecture;

        RuntimeInfoProvider(IDockerClient client, string operatingSystemType, string architecture)
        {
            this.client = Preconditions.CheckNotNull(client, nameof(client));

            this.operatingSystemType = string.IsNullOrWhiteSpace(operatingSystemType) ? CoreConstants.Unknown : operatingSystemType;
            this.architecture = string.IsNullOrWhiteSpace(architecture) ? CoreConstants.Unknown : architecture;
        }

        public async static Task<RuntimeInfoProvider> CreateAsync(IDockerClient client)
        {
            Preconditions.CheckNotNull(client, nameof(client));

            // get system information from docker
            SystemInfoResponse info = await client.System.GetSystemInfoAsync();

            return new RuntimeInfoProvider(client, info.OSType, info.Architecture);
        }

        public async Task<IEnumerable<ModuleRuntimeInfo>> GetModules(CancellationToken ctsToken)
        {
            var parameters = new ContainersListParameters
            {
                All = true,
                Filters = new Dictionary<string, IDictionary<string, bool>>
                {
                    { "label", Labels }
                }
            };
            IList<ContainerListResponse> containers = await this.client.Containers.ListContainersAsync(parameters, ctsToken);
            List<ContainerInspectResponse> containerInspectResponses = (await Task.WhenAll(containers.Select(c => this.client.Containers.InspectContainerAsync(c.ID)))).ToList();
            Option<ContainerInspectResponse> edgeAgentReponse = await this.GetEdgeAgentContainerAsync();
            edgeAgentReponse.ForEach(e => containerInspectResponses.Add(e));

            List<ModuleRuntimeInfo> modules = containerInspectResponses.Select(c => InspectResponseToModule(c)).ToList();
            return modules;
        }

        async Task<Option<ContainerInspectResponse>> GetEdgeAgentContainerAsync()
        {
            try
            {
                ContainerInspectResponse response = await this.client.Containers.InspectContainerAsync(CoreConstants.EdgeAgentModuleName);
                return Option.Some(response);
            }
            catch (DockerContainerNotFoundException ex)
            {
                Events.EdgeAgentContainerNotFound(ex);
                return Option.None<ContainerInspectResponse>();
            }
        }

        static (
            string name,
            int exitCode,
            string statusDescription,
            DateTime lastStartTime,
            DateTime lastExitTime,
            string imageHash
        )
        ExtractModuleRuntimeState(ContainerInspectResponse inspected)
        {
            string name = inspected.Name?.Substring(1) ?? CoreConstants.Unknown;
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

            if (!string.IsNullOrEmpty(lastExitTimeStr))
            {
                lastExitTime = DateTime.Parse(lastExitTimeStr, null, DateTimeStyles.RoundtripKind);
            }

            return (name, exitCode, statusDescription, lastStartTime, lastExitTime, inspected?.Image);
        }

        internal static ModuleRuntimeInfo InspectResponseToModule(ContainerInspectResponse inspectResponse)
        {
            // Get the following runtime state:
            //  - name
            //  - exit code
            //  - exit status description
            //  - last start time
            //  - last exit time
            //  - image hash
            (
            string name,
            int exitCode,
            string statusDescription,
            DateTime lastStartTime,
            DateTime lastExitTime,
            string imageHash
            ) = ExtractModuleRuntimeState(inspectResponse);

            var dockerConfig = new DockerReportedConfig(string.Empty, string.Empty, imageHash);

            // Figure out module stats and runtime status
            ModuleStatus runtimeStatus = ToRuntimeStatus(inspectResponse.State);

            var reportedConfig = new DockerReportedConfig(string.Empty, string.Empty, imageHash);
            var moduleRuntimeInfo = new ModuleRuntimeInfo<DockerReportedConfig>(name, "docker", runtimeStatus, statusDescription, exitCode, Option.Some(lastStartTime), Option.Some(lastExitTime), reportedConfig);
            return moduleRuntimeInfo;
        }

        static ModuleStatus ToRuntimeStatus(ContainerState containerState)
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

            return status;
        }

        public Task<SystemInfo> GetSystemInfo() => Task.FromResult(new SystemInfo(this.operatingSystemType, this.architecture));

        static class Events
        {
            static readonly ILogger Log = Util.Logger.Factory.CreateLogger<RuntimeInfoProvider>();
            const int IdStart = AgentEventIds.DockerEnvironment;
            static bool edgeAgentContainerNotFoundReported;

            enum EventIds
            {
                InvalidContainerStatus = IdStart,
                EdgeAgentContainerNotFound = IdStart + 1
            }

            public static void InvalidContainerStatusFound(string status)
            {
                Log.LogInformation((int)EventIds.InvalidContainerStatus, $"Encountered an unrecognized container state from Docker - {status}");
            }

            public static void EdgeAgentContainerNotFound(DockerContainerNotFoundException ex)
            {
                if (edgeAgentContainerNotFoundReported == false)
                {
                    Log.LogWarning((int)EventIds.EdgeAgentContainerNotFound, $"No container for edge agent was found with the name {CoreConstants.EdgeAgentModuleName} - {ex.Message}");
                    edgeAgentContainerNotFoundReported = true;
                }
            }
        }
    }
}
