// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Commands
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Docker.DotNet;
    using global::Docker.DotNet.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Binding = global::Docker.DotNet.Models.PortBinding;

    public class CreateCommand : ICommand
    {
        readonly IDockerClient client;
        readonly DockerModule module;

        public CreateCommand(IDockerClient client, DockerModule module)
        {
            this.client = Preconditions.CheckNotNull(client, nameof(client));
            this.module = Preconditions.CheckNotNull(module, nameof(module));
        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            var parameters = new CreateContainerParameters
            {
                Name = this.module.Name,
                Labels = new Dictionary<string, string> { { "version", this.module.Version } },
                Image = this.module.Config.Image + ":" + this.module.Config.Tag,
            };
            ApplyPortBindings(parameters, this.module);
            await this.client.Containers.CreateContainerAsync(parameters);
        }

        public Task UndoAsync(CancellationToken token) => TaskEx.Done;

        public string Show() => $"docker create {ShowPortBindings(this.module.Config.PortBindings)} --name {this.module.Name} --label version=\"{this.module.Version}\" {this.module.Config.Image}:{this.module.Config.Tag}";

        static string ShowPortBindings(IEnumerable<Docker.PortBinding> bindings) => string.Join(" ", bindings.Select(b => $"-p {b.To}:{b.From}"));

        static void ApplyPortBindings(CreateContainerParameters parameters, DockerModule module)
        {
            IDictionary<string, IList<Binding>> bindings = new Dictionary<string, IList<Binding>>();
            foreach (Docker.PortBinding binding in module.Config.PortBindings)
            {
                var pb = new Binding
                {
                    HostPort = binding.To
                };
                IList<Binding> current = bindings.GetOrElse(binding.From, () => new List<Binding>());
                current.Add(pb);
                bindings[$"{binding.From}/{TypeString(binding.Type)}"] = current;
            }

            parameters.HostConfig = parameters.HostConfig ?? new HostConfig();
            parameters.HostConfig.PortBindings = bindings;
        }

        static string TypeString(PortBindingType type)
        {
            switch (type)
            {
                case PortBindingType.Tcp:
                    return "tcp";
                case PortBindingType.Udp:
                    return "udp";
                default:
                    return "unknown";
            }
        }
    }
}