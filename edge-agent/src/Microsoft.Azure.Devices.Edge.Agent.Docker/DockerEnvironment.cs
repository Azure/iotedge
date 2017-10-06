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

    public class DockerEnvironment : IEnvironment
    {
        static readonly IDictionary<string, bool> Labels = new Dictionary<string, bool>
        {
            { $"{Constants.Labels.Owner}={Constants.Owner}", true }
        };

        readonly IDockerClient client;
        readonly IEntityStore<string, ModuleState> store;
        readonly IRestartPolicyManager restartManager;

        public DockerEnvironment(IDockerClient client, IEntityStore<string, ModuleState> store, IRestartPolicyManager restartManager)
        {
            this.client = Preconditions.CheckNotNull(client, nameof(client));
            this.store = Preconditions.CheckNotNull(store, nameof(store));
            this.restartManager = Preconditions.CheckNotNull(restartManager, nameof(restartManager));
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
            IModule[] modules = await Task.WhenAll(containers.Select(c => this.ContainerToModule(c)));
            return new ModuleSet(modules.ToDictionary(m => m.Name, m => m));
        }

        internal async Task<IModule> ContainerToModule(ContainerListResponse response)
        {
            string name = response.Names.FirstOrDefault()?.Substring(1) ?? "unknown";
            string version = response.Labels.GetOrElse(Constants.Labels.Version, "unknown");
            Core.RestartPolicy restartPolicy = (Core.RestartPolicy)Enum.Parse(
                typeof(Core.RestartPolicy),
                response.Labels.GetOrElse(
                    Constants.Labels.RestartPolicy,
                    Constants.DefaultRestartPolicy.ToString()
                )
            );
            ModuleStatus desiredStatus = (ModuleStatus)Enum.Parse(
                typeof(ModuleStatus),
                response.Labels.GetOrElse(
                    Constants.Labels.DesiredStatus,
                    Constants.DefaultDesiredStatus.ToString()
                )
            );

            string image = "unknown";
            string tag = string.Empty;
            if (response.Image != null)
            {
                // In case of local registries, the image name is something like localhost:5000/foo:latest
                // In that case, image = localhost:5000/foo and tag = latest
                int tagSplitterIndex = response.Image.LastIndexOf(':');
                if (tagSplitterIndex > 0)
                {
                    image = response.Image.Substring(0, tagSplitterIndex);
                    tag = response.Image.Substring(tagSplitterIndex + 1);
                }
                else
                {
                    image = response.Image;
                    // If the response.Image has no tag, then leave it as empty String, instead of defaulting to latest.
                }
            }

            ContainerInspectResponse inspected = await this.client.Containers.InspectContainerAsync(response.ID);
            int exitCode = (inspected?.State != null)? (int)inspected.State.ExitCode : 0;
            string statusDescription = inspected?.State?.Status;

            string lastStartTimeStr = inspected?.State?.StartedAt;
            DateTime lastStartTime = DateTime.MinValue;
            if(lastStartTimeStr != null)
            {
                lastStartTime = DateTime.Parse(lastStartTimeStr, null, DateTimeStyles.RoundtripKind);
            }

            string lastExitTimeStr = inspected?.State?.FinishedAt;
            DateTime lastExitTime = DateTime.MinValue;
            if(lastExitTime != null)
            {
                lastExitTime = DateTime.Parse(lastExitTimeStr, null, DateTimeStyles.RoundtripKind);
            }

            ModuleState restartState = (await this.store.Get(name)).GetOrElse(new ModuleState(0, lastExitTime));

            ModuleStatus runtimeStatus = ToRuntimeStatus(inspected.State, restartPolicy, restartState.RestartCount, lastExitTime);

            var config = new DockerConfig(image, tag, (inspected?.Config?.Labels?[Constants.Labels.NormalizedCreateOptions] ?? string.Empty));

            return new DockerRuntimeModule(
                name, version, desiredStatus, restartPolicy, config, exitCode,
                statusDescription, lastStartTime, lastExitTime,
                restartState.RestartCount, restartState.LastRestartTimeUtc, runtimeStatus
            );
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
            InvalidContainerStatus = IdStart
        }

        public static void InvalidContainerStatusFound(string status)
        {
            Log.LogInformation((int)EventIds.InvalidContainerStatus, $"Encountered an unrecognized container state from Docker - ${status}");
        }
    }
}
