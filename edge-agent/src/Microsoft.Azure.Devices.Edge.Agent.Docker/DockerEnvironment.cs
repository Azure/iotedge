// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Docker
{
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Docker.DotNet;
    using global::Docker.DotNet.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using Binding = global::Docker.DotNet.Models.PortBinding;
    using System;

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
            IEnumerable<PortBinding> portBindings = inspected
                ?.HostConfig
                ?.PortBindings
                ?.SelectMany(p => ToPortBinding(p.Key, p.Value)) ?? ImmutableList<PortBinding>.Empty;

            IDictionary<string, string> env = inspected
                ?.Config
                ?.Env
                ?.ToDictionary('=') ?? ImmutableDictionary<string, string>.Empty;

            int exitCode = (inspected?.State != null)? (int)inspected.State.ExitCode : 0;
            string statusDescription = inspected?.State?.Status;
            string lastStartTime = inspected?.State?.StartedAt;
            string lastExitTime = inspected?.State?.FinishedAt;

            var config = new DockerConfig(image, tag, portBindings, env);
            return new DockerEnvModule(name, version, status, config, exitCode, statusDescription, lastStartTime, lastExitTime);
        }

        static IEnumerable<PortBinding> ToPortBinding(string key, IList<Binding> binding)
        {
            string[] splits = key.Split('/');
            string fromStr = splits[0];
            string typeStr = splits.Length > 1 ? splits[1] : "tcp";

            if (splits.Length < 1)
            {
                Logger.LogWarning("Using default PortBinding type of 'tcp' for key '{0}'", key);
            }

            PortBindingType type;
            switch (typeStr.ToLowerInvariant())
            {
                case "tcp":
                    type = PortBindingType.Tcp;
                    break;
                case "udp":
                    type = PortBindingType.Udp;
                    break;
                default:
                    type = PortBindingType.Tcp;
                    break;
            }
            return binding.Select(b => new PortBinding(b.HostPort, fromStr, type));
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