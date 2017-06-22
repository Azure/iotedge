// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Commands
{
    using System;
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
        readonly DockerLoggingConfig dockerLoggerConfig;
        readonly Lazy<string> loggerOptionsLazy;
        readonly Lazy<string> envLazy;
        readonly Lazy<string> portBindingsLazy;
        readonly IConfigSource configSource;

        public CreateCommand(IDockerClient client, DockerModule module, DockerLoggingConfig dockerLoggerConfig, IConfigSource configSource)
        {
            this.client = Preconditions.CheckNotNull(client, nameof(client));
            this.module = Preconditions.CheckNotNull(module, nameof(module));
            this.dockerLoggerConfig = Preconditions.CheckNotNull(dockerLoggerConfig, nameof(dockerLoggerConfig));
            this.loggerOptionsLazy = new Lazy<string>(() => ShowLoggingOptions(this.dockerLoggerConfig));
            this.envLazy = new Lazy<string>(() => ShowEnvVars(this.GetContainerEnv()));
            this.portBindingsLazy = new Lazy<string>(() => ShowPortBindings(module.Config.PortBindings));
            this.configSource = Preconditions.CheckNotNull(configSource, nameof(configSource));
        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            var parameters = new CreateContainerParameters
            {
                Name = this.module.Name,
                Labels = new Dictionary<string, string>
                {
                    { "version", this.module.Version },
                    { "owner", Constants.Owner },
                },
                Image = this.module.Config.Image + ":" + this.module.Config.Tag,
                Env = this.GetContainerEnv().ToList()
            };
            ApplyPortBindings(parameters, this.module);
            ApplyLoggingOptions(parameters, this.dockerLoggerConfig);
            await this.client.Containers.CreateContainerAsync(parameters);
        }

        IEnumerable<string> GetContainerEnv()
        {
            IEnumerable<string> env = this.module.Config.Env.Select(kvp => $"{kvp.Key}={kvp.Value}");
            if (this.configSource.ContainsKey(Constants.EdgeHubConnectionStringKey))
            {
                string edgeHubConnectionString = this.configSource.GetValue<string>(Constants.EdgeHubConnectionStringKey).OrDefault();

                // append the module ID to this string
                edgeHubConnectionString = $"{Constants.EdgeHubConnectionStringKey}={edgeHubConnectionString};{Constants.ModuleIdKey}={this.module.Name}";

                env = env.Concat(new string[] { edgeHubConnectionString });
            }

            return env;
        }

        public Task UndoAsync(CancellationToken token) => TaskEx.Done;

        public string Show() => $"docker create {this.portBindingsLazy.Value} {this.envLazy.Value} {this.loggerOptionsLazy.Value} --name {this.module.Name} --label version=\"{this.module.Version}\" --label owner =\"{Constants.Owner}\" {this.module.Config.Image}:{this.module.Config.Tag}";

        static string ShowEnvVars(IEnumerable<string> env) => string.Join(" ", env.Select(val => $"--env \"{val}\""));

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

        static string ShowLoggingOptions(DockerLoggingConfig dockerLoggerConfig) => 
            string.Join(" ", "--logdriver", dockerLoggerConfig.Type, string.Join(" ", dockerLoggerConfig.Config.Select(kvp => $"--log-opt \"{kvp.Key}={kvp.Value}\"")));


        static void ApplyLoggingOptions(CreateContainerParameters parameters, DockerLoggingConfig dockerLoggerConfig)
        {
            parameters.HostConfig = parameters.HostConfig ?? new HostConfig();
            parameters.HostConfig.LogConfig = parameters.HostConfig.LogConfig ?? new LogConfig();
            parameters.HostConfig.LogConfig.Type = dockerLoggerConfig.Type;
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