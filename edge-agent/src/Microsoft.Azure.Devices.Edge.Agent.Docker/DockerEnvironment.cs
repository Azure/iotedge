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

    public class DockerEnvironment : IEnvironment
    {
        readonly IDockerClient client;

        public DockerEnvironment(IDockerClient client)
        {
            this.client = Preconditions.CheckNotNull(client, nameof(client));
        }

        public async Task<ModuleSet> GetModulesAsync(CancellationToken token)
        {
            var parameters = new ContainersListParameters
            {
                All = true
            };
            IList<ContainerListResponse> containers = await this.client.Containers.ListContainersAsync(parameters);
            IDictionary<string, IModule> modules = containers.Select(c => ContainerToModule(c)).ToDictionary(m => m.Name, m => m);
            return new ModuleSet(modules);
        }

        static IModule ContainerToModule(ContainerListResponse response)
        {
            string name = response.Names.FirstOrDefault()?.Substring(1) ?? "unknown";
            string version = response.Labels.GetOrElse("version", "unknown");
            ModuleStatus status = ToStatus(response.State);

            string[] imageParts = (response.Image ?? "unknown").Split(':');
            string image = imageParts[0];
            string tag = imageParts.Length > 1 ? imageParts[1] : "latest";
            var config = new DockerConfig(image, tag);
            return new DockerModule(name, version, status, config);
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