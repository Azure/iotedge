// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Docker.DotNet;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Docker.Models;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;
    using Newtonsoft.Json;
    using DockerModels = global::Docker.DotNet.Models;

    public class CreateCommand : ICommand
    {
        static readonly Dictionary<string, PortBinding> EdgeHubPortBinding = new Dictionary<string, PortBinding>
        {
            { "8883/tcp", new PortBinding { HostPort = "8883" } },
            { "443/tcp", new PortBinding { HostPort = "443" } }
        };

        readonly CreateContainerParameters createContainerParameters;
        readonly IDockerClient client;

        readonly Lazy<string> id;

        public CreateCommand(IDockerClient client, CreateContainerParameters createContainerParameters)
        {
            this.client = Preconditions.CheckNotNull(client, nameof(client));
            this.createContainerParameters = Preconditions.CheckNotNull(createContainerParameters, nameof(createContainerParameters));
            this.id = new Lazy<string>(() => JsonConvert.SerializeObject(this.createContainerParameters).CreateSha256());
        }

        // We use the hash code of the JSONified representation of the create parameters as the
        // unique "ID" for this command.
        public string Id => this.id.Value;

        public static async Task<ICommand> BuildAsync(
            IDockerClient client,
            DockerModule module,
            IModuleIdentity identity,
            DockerLoggingConfig defaultDockerLoggerConfig,
            IConfigSource configSource,
            bool buildForEdgeHub)
        {
            // Validate parameters
            Preconditions.CheckNotNull(client, nameof(client));
            Preconditions.CheckNotNull(module, nameof(module));
            Preconditions.CheckNotNull(defaultDockerLoggerConfig, nameof(defaultDockerLoggerConfig));
            Preconditions.CheckNotNull(configSource, nameof(configSource));

            CreateContainerParameters createContainerParameters = module.Config.CreateOptions ?? new CreateContainerParameters();

            // serialize user provided create options to add as docker label, before adding other values
            string createOptionsString = JsonConvert.SerializeObject(createContainerParameters);

            // Force update parameters with indexing entries
            createContainerParameters.Name = module.Name;
            createContainerParameters.Image = module.Config.Image;

            DeploymentConfigInfo deploymentConfigInfo = await configSource.GetDeploymentConfigInfoAsync();
            DeploymentConfig deploymentConfig = deploymentConfigInfo.DeploymentConfig;
            Option<DockerRuntimeInfo> dockerRuntimeInfo = deploymentConfig != DeploymentConfig.Empty && deploymentConfig.Runtime is DockerRuntimeInfo
                ? Option.Some((DockerRuntimeInfo)deploymentConfig.Runtime)
                : Option.None<DockerRuntimeInfo>();

            // Inject global parameters
            InjectCerts(createContainerParameters, configSource, buildForEdgeHub);
            InjectConfig(createContainerParameters, identity, buildForEdgeHub, configSource);
            InjectPortBindings(createContainerParameters, buildForEdgeHub);
            InjectLoggerConfig(createContainerParameters, defaultDockerLoggerConfig, dockerRuntimeInfo.Map(r => r.Config.LoggingOptions));
            InjectModuleEnvVars(createContainerParameters, module.Env);
            // Inject required Edge parameters
            InjectLabels(createContainerParameters, module, createOptionsString);

            InjectNetworkAlias(createContainerParameters, configSource, buildForEdgeHub);

            return new CreateCommand(client, createContainerParameters);
        }

        public Task ExecuteAsync(CancellationToken token)
        {
            // Do a serialization roundtrip to convert the Edge.Agent.Docker.Models.CreateContainerParameters to Docker.DotNet.Models.CreateContainerParameters
            //
            // This will lose properties in the former that are not defined in the latter, but this code path is only for the old Docker mode anyway.
            var createContainerParameters =
                JsonConvert.DeserializeObject<DockerModels.CreateContainerParameters>(JsonConvert.SerializeObject(this.createContainerParameters));

            // Copy the Name property manually. See the docs of Edge.Agent.Docker.Models.CreateContainerParameters' Name property for an explanation.
            createContainerParameters.Name = this.createContainerParameters.Name;

            return this.client.Containers.CreateContainerAsync(createContainerParameters, token);
        }

        public string Show() => $"docker create --name {this.createContainerParameters.Name} {this.createContainerParameters.Image}";

        public Task UndoAsync(CancellationToken token) => TaskEx.Done;

        internal static void InjectPortBindings(CreateContainerParameters createContainerParameters, bool injectForEdgeHub)
        {
            if (injectForEdgeHub)
            {
                createContainerParameters.HostConfig = createContainerParameters.HostConfig ?? new HostConfig();
                createContainerParameters.HostConfig.PortBindings = createContainerParameters.HostConfig.PortBindings ?? new Dictionary<string, IList<PortBinding>>();

                foreach (KeyValuePair<string, PortBinding> binding in EdgeHubPortBinding)
                {
                    IList<PortBinding> current = createContainerParameters.HostConfig.PortBindings.GetOrElse(binding.Key, () => new List<PortBinding>());
                    if (!current.Any(p => p.HostPort.Equals(binding.Value.HostPort, StringComparison.OrdinalIgnoreCase)))
                    {
                        current.Add(binding.Value);
                    }

                    createContainerParameters.HostConfig.PortBindings[binding.Key] = current;
                }
            }
        }

        static void InjectConfig(CreateContainerParameters createContainerParameters, IModuleIdentity identity, bool injectForEdgeHub, IConfigSource configSource)
        {
            var envVars = new List<string>();

            // Inject the connection string as an environment variable
            if (identity.Credentials is ConnectionStringCredentials creds && !string.IsNullOrWhiteSpace(creds.ConnectionString))
            {
                string connectionStringKey = injectForEdgeHub ? Constants.IotHubConnectionStringKey : Constants.EdgeHubConnectionStringKey;
                envVars.Add($"{connectionStringKey}={creds.ConnectionString}");
            }

            if (injectForEdgeHub)
            {
                envVars.Add($"{Logger.RuntimeLogLevelEnvKey}={Logger.GetLogLevel()}");
            }

            configSource.Configuration.GetValue<string>(Constants.UpstreamProtocolKey).ToUpstreamProtocol().ForEach(
                u =>
                {
                    if (createContainerParameters.Env?.Any(e => e.StartsWith("UpstreamProtocol=", StringComparison.OrdinalIgnoreCase)) == false)
                    {
                        envVars.Add($"UpstreamProtocol={u}");
                    }
                });

            InjectEnvVars(createContainerParameters, envVars);
        }

        static void InjectLoggerConfig(CreateContainerParameters createContainerParameters, DockerLoggingConfig defaultDockerLoggerConfig, Option<string> sourceLoggingOptions)
        {
            createContainerParameters.HostConfig = createContainerParameters.HostConfig ?? new HostConfig();

            Option<LogConfig> sourceOptions;
            try
            {
                sourceOptions = sourceLoggingOptions.Filter(l => !string.IsNullOrEmpty(l)).Map(
                    l =>
                        JsonConvert.DeserializeObject<LogConfig>(l));
            }
            catch
            {
                sourceOptions = Option.None<LogConfig>();
            }

            if (createContainerParameters.HostConfig.LogConfig == null || string.IsNullOrWhiteSpace(createContainerParameters.HostConfig.LogConfig.Type))
            {
                createContainerParameters.HostConfig.LogConfig = sourceOptions.GetOrElse(
                    new LogConfig
                    {
                        Type = defaultDockerLoggerConfig.Type,
                        Config = defaultDockerLoggerConfig.Config
                    });
            }
        }

        static void InjectLabels(CreateContainerParameters createContainerParameters, DockerModule module, string createOptionsString)
        {
            // Inject required Edge parameters
            createContainerParameters.Labels = createContainerParameters.Labels ?? new Dictionary<string, string>();

            createContainerParameters.Labels[Constants.Labels.Owner] = Constants.OwnerValue;
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
            string networkId = configSource.Configuration.GetValue<string>(Constants.NetworkIdKey);
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

        static void InjectVolume(CreateContainerParameters createContainerParameters, string volumeName, string volumePath, bool readOnly = true)
        {
            if (!string.IsNullOrWhiteSpace(volumeName) && !string.IsNullOrWhiteSpace(volumePath))
            {
                HostConfig hostConfig = createContainerParameters.HostConfig = createContainerParameters.HostConfig ?? new HostConfig();
                hostConfig.Binds = hostConfig.Binds ?? new List<string>();

                string ro = readOnly ? ":ro" : string.Empty;
                hostConfig.Binds.Add($"{volumeName}:{volumePath}{ro}");
            }
        }

        static void InjectCerts(CreateContainerParameters createContainerParameters, IConfigSource configSource, bool injectForEdgeHub)
        {
            createContainerParameters.HostConfig = createContainerParameters.HostConfig ?? new HostConfig();
            var varsList = new List<string>();
            if (injectForEdgeHub)
            {
                // for the EdgeHub we need to inject the CA chain cert that was used to sign the Hub server certificate
                string moduleCaChainCertFile = configSource.Configuration.GetValue(Constants.EdgeModuleHubServerCaChainCertificateFileKey, string.Empty);
                if (string.IsNullOrWhiteSpace(moduleCaChainCertFile) == false)
                {
                    varsList.Add($"{Constants.EdgeModuleHubServerCaChainCertificateFileKey}={moduleCaChainCertFile}");
                }

                // for the EdgeHub we also need to inject the Hub server certificate which will be used for TLS connections
                // from modules and leaf devices
                string moduleHubCertFile = configSource.Configuration.GetValue(Constants.EdgeModuleHubServerCertificateFileKey, string.Empty);
                if (string.IsNullOrWhiteSpace(moduleHubCertFile) == false)
                {
                    varsList.Add($"{Constants.EdgeModuleHubServerCertificateFileKey}={moduleHubCertFile}");
                }

                // mount edge hub volume
                InjectVolume(
                    createContainerParameters,
                    configSource.Configuration.GetValue(Constants.EdgeHubVolumeNameKey, string.Empty),
                    configSource.Configuration.GetValue(Constants.EdgeHubVolumePathKey, string.Empty));
            }
            else
            {
                // for all Edge modules, the agent should inject the CA certificate that can be used for Edge Hub server certificate
                // validation
                string moduleCaCertFile = configSource.Configuration.GetValue(Constants.EdgeModuleCaCertificateFileKey, string.Empty);
                if (string.IsNullOrWhiteSpace(moduleCaCertFile) == false)
                {
                    varsList.Add($"{Constants.EdgeModuleCaCertificateFileKey}={moduleCaCertFile}");
                }

                // mount module volume
                InjectVolume(
                    createContainerParameters,
                    configSource.Configuration.GetValue(Constants.EdgeModuleVolumeNameKey, string.Empty),
                    configSource.Configuration.GetValue(Constants.EdgeModuleVolumePathKey, string.Empty));
            }

            InjectEnvVars(createContainerParameters, varsList);
        }

        static void InjectModuleEnvVars(
            CreateContainerParameters createContainerParameters,
            IDictionary<string, EnvVal> moduleEnvVars)
        {
            var envVars = new List<string>();
            foreach (KeyValuePair<string, EnvVal> envVar in moduleEnvVars)
            {
                envVars.Add($"{envVar.Key}={envVar.Value.Value}");
            }

            InjectEnvVars(createContainerParameters, envVars);
        }

        static void InjectEnvVars(
            CreateContainerParameters createContainerParameters,
            IList<string> varsList)
        {
            createContainerParameters.Env = createContainerParameters.Env?.RemoveIntersectionKeys(varsList).ToList() ?? new List<string>();
            foreach (string envVar in varsList)
            {
                createContainerParameters.Env.Add(envVar);
            }
        }
    }
}
