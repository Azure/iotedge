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
    using Microsoft.Extensions.Configuration;
    using Newtonsoft.Json;

    public class CreateCommand : ICommand
    {
        readonly CreateContainerParameters createContainerParameters;
        readonly IDockerClient client;

        public CreateCommand(IDockerClient client, DockerModule module, DockerLoggingConfig dockerLoggerConfig, IConfigSource configSource)
        {
            // Validate parameters
            this.client = Preconditions.CheckNotNull(client, nameof(client));
            Preconditions.CheckNotNull(module, nameof(module));
            Preconditions.CheckNotNull(dockerLoggerConfig, nameof(dockerLoggerConfig));
            Preconditions.CheckNotNull(configSource, nameof(configSource));

            this.createContainerParameters = module.Config.CreateOptions ?? new CreateContainerParameters();
            var normalizedCreateOptions = JsonConvert.SerializeObject(this.createContainerParameters);

            // Force update parameters with indexing entries
            this.createContainerParameters.Name = module.Name;
            this.createContainerParameters.Image = module.Config.Image + ":" + module.Config.Tag;

            // Inject global parameters
            InjectConfig(this.createContainerParameters, configSource, module);
            InjectLoggerConfig(this.createContainerParameters, dockerLoggerConfig);

            // Inject required Edge parameters
            this.createContainerParameters.Labels = this.createContainerParameters.Labels ?? new Dictionary<string, string>();
            
            this.createContainerParameters.Labels.Remove(Constants.Labels.Owner);
            this.createContainerParameters.Labels.Add(Constants.Labels.Owner, Constants.Owner);
            
            this.createContainerParameters.Labels.Remove(Constants.Labels.Version);
            this.createContainerParameters.Labels.Add(Constants.Labels.Version, module.Version);
            
            this.createContainerParameters.Labels.Remove(Constants.Labels.NormalizedCreateOptions);
            this.createContainerParameters.Labels.Add(Constants.Labels.NormalizedCreateOptions, normalizedCreateOptions);

            this.createContainerParameters.Labels.Remove(Constants.Labels.RestartPolicy);
            this.createContainerParameters.Labels.Add(Constants.Labels.RestartPolicy, module.RestartPolicy.ToString());

            this.createContainerParameters.Labels.Remove(Constants.Labels.DesiredStatus);
            this.createContainerParameters.Labels.Add(Constants.Labels.DesiredStatus, module.DesiredStatus.ToString());
        }

        public Task ExecuteAsync(CancellationToken token) => this.client.Containers.CreateContainerAsync(this.createContainerParameters, token);

        public string Show() => $"docker create {ObfuscateConnectionStringInCreateContainerParameters(JsonConvert.SerializeObject(this.createContainerParameters))}";

        public Task UndoAsync(CancellationToken token) => TaskEx.Done;

        static void InjectConfig(CreateContainerParameters parameters, IConfigSource configSource, DockerModule module)
        {
            // Inject the connection string as an environment variable
            string edgeHubConnectionString = configSource.Configuration.GetValue(Constants.EdgeHubConnectionStringKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(edgeHubConnectionString))
            {
                parameters.Env = parameters.Env ?? new List<string>();
                parameters.Env.Remove($"{Constants.EdgeHubConnectionStringKey}={edgeHubConnectionString};{Constants.ModuleIdKey}={module.Name}");
                parameters.Env.Add($"{Constants.EdgeHubConnectionStringKey}={edgeHubConnectionString};{Constants.ModuleIdKey}={module.Name}");
            }
        }

        static void InjectLoggerConfig(CreateContainerParameters parameters, DockerLoggingConfig dockerLoggerConfig)
        {
            parameters.HostConfig = parameters.HostConfig ?? new HostConfig();
            parameters.HostConfig.LogConfig = parameters.HostConfig.LogConfig ?? new LogConfig();
            parameters.HostConfig.LogConfig.Type = dockerLoggerConfig.Type;
            parameters.HostConfig.LogConfig.Config = dockerLoggerConfig.Config;
        }

        static string ObfuscateConnectionStringInCreateContainerParameters(string serializedCreateOptions)
        {
            var scrubbed = JsonConvert.DeserializeObject<CreateContainerParameters>(serializedCreateOptions);
            scrubbed.Env?.Select((env, i) => ((env.IndexOf(Constants.EdgeHubConnectionStringKey) == -1) ? env : $"{Constants.EdgeHubConnectionStringKey}=******"));
            return JsonConvert.SerializeObject(scrubbed);
        }
    }
}
