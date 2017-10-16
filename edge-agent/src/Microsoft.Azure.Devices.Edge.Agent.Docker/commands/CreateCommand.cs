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

        public CreateCommand(IDockerClient client, CreateContainerParameters createContainerParameters)
        {
            this.client = Preconditions.CheckNotNull(client, nameof(client));
            this.createContainerParameters = Preconditions.CheckNotNull(createContainerParameters, nameof(createContainerParameters));
        }

        public static ICommand Build(IDockerClient client, DockerModule module, IModuleIdentity identity, DockerLoggingConfig dockerLoggerConfig, IConfigSource configSource, bool buildForEdgeHub)
        {
            // Validate parameters
            Preconditions.CheckNotNull(client, nameof(client));
            Preconditions.CheckNotNull(module, nameof(module));
            Preconditions.CheckNotNull(dockerLoggerConfig, nameof(dockerLoggerConfig));
            Preconditions.CheckNotNull(configSource, nameof(configSource));

            CreateContainerParameters createContainerParameters = module.Config.CreateOptions ?? new CreateContainerParameters();

            // serialize user provided create options to add as docker label, before adding other values
            string createOptionsString = JsonConvert.SerializeObject(createContainerParameters);

            // Force update parameters with indexing entries
            createContainerParameters.Name = module.Name;
            createContainerParameters.Image = module.Config.Image;

            // Inject global parameters
            InjectConfig(createContainerParameters, identity, buildForEdgeHub);
            InjectLoggerConfig(createContainerParameters, dockerLoggerConfig);

            // Inject required Edge parameters
            InjectLabels(createContainerParameters, module, createOptionsString);

            InjectNetworkAlias(createContainerParameters, configSource, buildForEdgeHub);

            return new CreateCommand(client, createContainerParameters);
        }

        public Task ExecuteAsync(CancellationToken token) => this.client.Containers.CreateContainerAsync(this.createContainerParameters, token);

        public string Show() => $"docker create {ObfuscateConnectionStringInCreateContainerParameters(JsonConvert.SerializeObject(this.createContainerParameters))}";

        public Task UndoAsync(CancellationToken token) => TaskEx.Done;

        static void InjectConfig(CreateContainerParameters createContainerParameters, IModuleIdentity identity, bool injectForEdgeHub)
        {
            // Inject the connection string as an environment variable
            if (!string.IsNullOrWhiteSpace(identity.ConnectionString))
            {
                string connectionStringKey = injectForEdgeHub ? Constants.IotHubConnectionStringKey : Constants.EdgeHubConnectionStringKey;
                string edgeDeviceConnectionString = $"{connectionStringKey}={identity.ConnectionString}";
                
                if(createContainerParameters.Env != null)
                {
                    // Remove any existing environment variables with the same key.
                    List<string> existingConnectionStrings = createContainerParameters.Env.Where(e => e.StartsWith($"{connectionStringKey}=")).ToList();
                    existingConnectionStrings.ForEach(e => createContainerParameters.Env.Remove(e));
                }
                else
                {
                    createContainerParameters.Env = new List<string>();
                }
                createContainerParameters.Env.Add(edgeDeviceConnectionString);
            }
        }

        static void InjectLoggerConfig(CreateContainerParameters parameters, DockerLoggingConfig dockerLoggerConfig)
        {
            parameters.HostConfig = parameters.HostConfig ?? new HostConfig();
            parameters.HostConfig.LogConfig = parameters.HostConfig.LogConfig ?? new LogConfig();
            parameters.HostConfig.LogConfig.Type = dockerLoggerConfig.Type;
            parameters.HostConfig.LogConfig.Config = dockerLoggerConfig.Config;
        }

        static void InjectLabels(CreateContainerParameters createContainerParameters, DockerModule module, string createOptionsString)
        {
            // Inject required Edge parameters
            createContainerParameters.Labels = createContainerParameters.Labels ?? new Dictionary<string, string>();

            createContainerParameters.Labels[Constants.Labels.Owner] = Constants.Owner;
            createContainerParameters.Labels[Constants.Labels.NormalizedCreateOptions] = createOptionsString;
            createContainerParameters.Labels[Constants.Labels.RestartPolicy] = module.RestartPolicy.ToString();
            createContainerParameters.Labels[Constants.Labels.DesiredStatus] = module.DesiredStatus.ToString();

            if (!string.IsNullOrWhiteSpace(module.Version))
            {
                createContainerParameters.Labels[Constants.Labels.Version] = module.Version;
            }

            if (!string.IsNullOrWhiteSpace(module.ConfigurationInfo.Id))
            {
                createContainerParameters.Labels[Constants.Labels.ConfigurationId] = module.ConfigurationInfo.Id;
            }
        }

        static void InjectNetworkAlias(CreateContainerParameters createContainerParameters, IConfigSource configSource, bool addEdgeDeviceHostNameAlias)
        {
            string networkId = configSource.Configuration.GetValue<string>(Docker.Constants.NetworkIdKey);
            string edgeDeviceHostName = configSource.Configuration.GetValue<string>(Constants.EdgeDeviceHostNameKey);
            if (!string.IsNullOrWhiteSpace(networkId))
            {
                var endpointSettings = new EndpointSettings();
                if (addEdgeDeviceHostNameAlias && !string.IsNullOrWhiteSpace(edgeDeviceHostName))
                {
                    endpointSettings.Aliases = new List<string> { edgeDeviceHostName };
                }

                IDictionary<string, EndpointSettings> endpointsConfig = new Dictionary<string, EndpointSettings>
                {
                    [networkId] = endpointSettings
                };
                createContainerParameters.NetworkingConfig = new NetworkingConfig { EndpointsConfig = endpointsConfig };
            }

        }

        static string ObfuscateConnectionStringInCreateContainerParameters(string serializedCreateOptions)
        {
            var scrubbed = JsonConvert.DeserializeObject<CreateContainerParameters>(serializedCreateOptions);
            scrubbed.Env = scrubbed.Env?
                .Select((env, i) => env.IndexOf(Constants.EdgeHubConnectionStringKey) == -1 ? env : $"{Constants.EdgeHubConnectionStringKey}=******")
                .Select((env, i) => env.IndexOf(Constants.IotHubConnectionStringKey) == -1 ? env : $"{Constants.IotHubConnectionStringKey}=******")
                .ToList();
            return JsonConvert.SerializeObject(scrubbed);
        }
    }
}
