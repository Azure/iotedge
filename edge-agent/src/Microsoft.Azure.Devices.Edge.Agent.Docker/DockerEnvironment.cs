// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Docker
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Docker.DotNet;
    using global::Docker.DotNet.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class DockerEnvironment : IEnvironment
    {
        static readonly ILogger Logger = Util.Logger.Factory.CreateLogger<DockerEnvironment>();

        static readonly IDictionary<string, bool> Labels = new Dictionary<string, bool>
        {
            { $"owner={Constants.Owner}", true }
        };

        readonly IDockerClient client;

        public DockerEnvironment(IDockerClient client)
        {
            this.client = Preconditions.CheckNotNull(client, nameof(client));
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
            string version = response.Labels.GetOrElse("version", "unknown");
            ModuleStatus status = ToStatus(response.State);

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
            string lastStartTime = inspected?.State?.StartedAt;
            string lastExitTime = inspected?.State?.FinishedAt;

            var config = new DockerConfig(image, tag, (inspected?.Config?.Labels?["normalizedCreateOptions"] ?? string.Empty));
            return new DockerEnvModule(name, version, status, config, exitCode, statusDescription, lastStartTime, lastExitTime);
        }

        static ModuleStatus ToStatus(string state)
        {
            switch (state.ToLower())
            {
                case "created":
                    return ModuleStatus.Stopped;
                case "restarting":
                    return ModuleStatus.Stopped;
                case "running":
                    return ModuleStatus.Running;
                case "paused":
                    return ModuleStatus.Paused;
                case "exited":
                    return ModuleStatus.Stopped;
                default:
                    return ModuleStatus.Unknown;
            }
        }
    }
}