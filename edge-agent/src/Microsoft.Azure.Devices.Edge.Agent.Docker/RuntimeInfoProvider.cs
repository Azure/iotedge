// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Docker.DotNet;
    using global::Docker.DotNet.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using CoreConstants = Microsoft.Azure.Devices.Edge.Agent.Core.Constants;

    public class RuntimeInfoProvider : IRuntimeInfoProvider
    {
        static readonly IDictionary<string, bool> Labels = new Dictionary<string, bool>
        {
            { $"{CoreConstants.Labels.Owner}={CoreConstants.OwnerValue}", true }
        };

        readonly IDockerClient client;
        readonly string operatingSystemType;
        readonly string architecture;
        readonly string version;

        RuntimeInfoProvider(IDockerClient client, string operatingSystemType, string architecture, string version)
        {
            this.client = Preconditions.CheckNotNull(client, nameof(client));

            this.operatingSystemType = string.IsNullOrWhiteSpace(operatingSystemType) ? CoreConstants.Unknown : operatingSystemType;
            this.architecture = string.IsNullOrWhiteSpace(architecture) ? CoreConstants.Unknown : architecture;
            this.version = string.IsNullOrWhiteSpace(version) ? CoreConstants.Unknown : version;
        }

        public static async Task<RuntimeInfoProvider> CreateAsync(IDockerClient client)
        {
            Preconditions.CheckNotNull(client, nameof(client));

            // get system information from docker
            SystemInfoResponse info = await client.System.GetSystemInfoAsync();

            return new RuntimeInfoProvider(client, info.OSType, info.Architecture, info.ServerVersion);
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

            Option<ContainerInspectResponse>[] containerInspectResponses =
                await Task.WhenAll(
                         containers.Select(
                                        c => this.client.Containers
                                                 .InspectContainerAsync(c.ID)
                                                 .MayThrow(typeof(DockerContainerNotFoundException)))
                                    .Concat(new[]
                                    {
                                        this.client.Containers
                                                 .InspectContainerAsync(CoreConstants.EdgeAgentModuleName)
                                                 .MayThrow(EdgeAgentNotFoundAlternative, typeof(DockerContainerNotFoundException))
                                    }));

            List<ModuleRuntimeInfo> modules = containerInspectResponses
                                                 .FilterMap()
                                                 .Select(InspectResponseToModule)
                                                 .ToList();

            return modules;
        }

        public Task<Stream> GetModuleLogs(string module, bool follow, Option<int> tail, Option<int> since, CancellationToken cancellationToken)
        {
            var containerLogsParameters = new ContainerLogsParameters
            {
                Follow = follow,
                ShowStderr = true,
                ShowStdout = true
            };
            tail.ForEach(t => containerLogsParameters.Tail = t.ToString());
            since.ForEach(t => containerLogsParameters.Since = t.ToString());
            return this.client.Containers.GetContainerLogsAsync(module, containerLogsParameters, cancellationToken);
        }

        public Task<SystemInfo> GetSystemInfo() => Task.FromResult(new SystemInfo(this.operatingSystemType, this.architecture, this.version));

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
                string imageHash,
                string image
            ) = ExtractModuleRuntimeState(inspectResponse);

            // Figure out module stats and runtime status
            ModuleStatus runtimeStatus = ToRuntimeStatus(inspectResponse.State);

            var reportedConfig = new DockerReportedConfig(image, string.Empty, imageHash);
            var moduleRuntimeInfo = new ModuleRuntimeInfo<DockerReportedConfig>(name, "docker", runtimeStatus, statusDescription, exitCode, Option.Some(lastStartTime), Option.Some(lastExitTime), reportedConfig);
            return moduleRuntimeInfo;
        }

        static (
            string name,
            int exitCode,
            string statusDescription,
            DateTime lastStartTime,
            DateTime lastExitTime,
            string imageHash,
            string image
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

            string image = inspected.Config.Image;
            string hash = inspected?.Image;

            return (name, exitCode, statusDescription, lastStartTime, lastExitTime, hash, image);
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

        static Option<ContainerInspectResponse> EdgeAgentNotFoundAlternative(Exception ex)
        {
                Events.EdgeAgentContainerNotFound(ex);
                return Option.None<ContainerInspectResponse>();
        }

        static class Events
        {
            const int IdStart = AgentEventIds.DockerEnvironment;
            static readonly ILogger Log = Logger.Factory.CreateLogger<RuntimeInfoProvider>();
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

            public static void EdgeAgentContainerNotFound(Exception ex)
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
