// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Edgelet.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Edgelet.GeneratedCode;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;
    using Newtonsoft.Json;

    public class CreateOrUpdateCommand : ICommand
    {
        readonly IModuleManager moduleManager;
        readonly ModuleSpec moduleSpec;
        readonly Lazy<string> id;
        readonly Operation operation;

        CreateOrUpdateCommand(IModuleManager moduleManager, ModuleSpec moduleSpec, Operation operation)
        {
            this.moduleManager = Preconditions.CheckNotNull(moduleManager, nameof(moduleManager));
            this.moduleSpec = Preconditions.CheckNotNull(moduleSpec, nameof(moduleSpec));
            this.id = new Lazy<string>(() => JsonConvert.SerializeObject(this.moduleSpec).CreateSha256());
            this.operation = operation;
        }

        public static CreateOrUpdateCommand BuildCreate(
            IModuleManager moduleManager,
            IModule module,
            IModuleIdentity identity,
            IConfigSource configSource,
            object settings) =>
            Build(moduleManager, module, identity, configSource, settings, Operation.Create);

        public static CreateOrUpdateCommand BuildUpdate(
            IModuleManager moduleManager,
            IModule module,
            IModuleIdentity identity,
            IConfigSource configSource,
            object settings,
            bool start) =>
            Build(moduleManager, module, identity, configSource, settings, start ? Operation.UpdateAndStart : Operation.Update);

        static CreateOrUpdateCommand Build(IModuleManager moduleManager, IModule module, IModuleIdentity identity,
            IConfigSource configSource, object settings, Operation operation)
        {
            Preconditions.CheckNotNull(moduleManager, nameof(moduleManager));
            Preconditions.CheckNotNull(module, nameof(module));
            Preconditions.CheckNotNull(identity, nameof(identity));
            Preconditions.CheckNotNull(configSource, nameof(configSource));
            Preconditions.CheckNotNull(settings, nameof(settings));

            IEnumerable<EnvVar> envVars = GetEnvVars(module.Env, identity, configSource);
            ModuleSpec moduleSpec = BuildModuleSpec(module, envVars, settings);
            return new CreateOrUpdateCommand(moduleManager, moduleSpec, operation);
        }

        public string Id => this.id.Value;

        public Task ExecuteAsync(CancellationToken token)
        {
            switch (this.operation)
            {
                case Operation.Update:
                    return this.moduleManager.UpdateModuleAsync(this.moduleSpec);

                case Operation.UpdateAndStart:
                    return this.moduleManager.UpdateAndStartModuleAsync(this.moduleSpec);

                default:
                    return this.moduleManager.CreateModuleAsync(this.moduleSpec);
            }
        }

        public string Show()
        {
            switch (this.operation)
            {
                case Operation.Update:
                    return $"Update module {this.moduleSpec.Name}";

                case Operation.UpdateAndStart:
                    return $"Update and start module {this.moduleSpec.Name}";

                default:
                    return $"Create module {this.moduleSpec.Name}";
            }
        }

        public Task UndoAsync(CancellationToken token) => TaskEx.Done;

        internal static ModuleSpec BuildModuleSpec(IModule module, IEnumerable<EnvVar> envVars, object settings)
        {
            var moduleSpec = new ModuleSpec
            {
                Name = module.Name,
                Config = new Config
                {
                    Settings = settings,
                    Env = new ObservableCollection<EnvVar>(envVars)
                },
                Type = module.Type
            };
            return moduleSpec;
        }

        internal static IEnumerable<EnvVar> GetEnvVars(IDictionary<string, EnvVal> moduleEnvVars, IModuleIdentity identity, IConfigSource configSource)
        {
            List<EnvVar> envVars = moduleEnvVars.Select(m => new EnvVar { Key = m.Key, Value = m.Value.Value }).ToList();

            // Inject the connection details as an environment variable
            if (identity.Credentials is IdentityProviderServiceCredentials creds)
            {
                if (!string.IsNullOrWhiteSpace(creds.ProviderUri))
                {
                    envVars.Add(new EnvVar { Key = Constants.EdgeletWorkloadUriVariableName, Value = creds.ProviderUri });
                }

                if (!string.IsNullOrWhiteSpace(creds.AuthScheme))
                {
                    envVars.Add(new EnvVar { Key = Constants.EdgeletAuthSchemeVariableName, Value = creds.AuthScheme });
                }

                if (!string.IsNullOrWhiteSpace(creds.ModuleGenerationId))
                {
                    envVars.Add(new EnvVar { Key = Constants.EdgeletModuleGenerationIdVariableName, Value = creds.ModuleGenerationId });
                }
            }

            if (!string.IsNullOrWhiteSpace(identity.IotHubHostname))
            {
                envVars.Add(new EnvVar { Key = Constants.IotHubHostnameVariableName, Value = identity.IotHubHostname });
            }

            if (!string.IsNullOrWhiteSpace(identity.GatewayHostname))
            {
                if (identity.ModuleId.Equals(Constants.EdgeAgentModuleIdentityName) || identity.ModuleId.Equals(Constants.EdgeHubModuleIdentityName))
                {
                    envVars.Add(
                        new EnvVar
                        {
                            Key = Constants.EdgeDeviceHostNameKey,
                            Value = identity.GatewayHostname
                        });
                }
                else if (!identity.ModuleId.Equals(Constants.EdgeHubModuleIdentityName))
                {
                    envVars.Add(
                        new EnvVar
                        {
                            Key = Constants.GatewayHostnameVariableName,
                            Value = identity.GatewayHostname
                        });
                }
            }

            if (!string.IsNullOrWhiteSpace(identity.DeviceId))
            {
                envVars.Add(new EnvVar { Key = Constants.DeviceIdVariableName, Value = identity.DeviceId });
            }

            if (!string.IsNullOrWhiteSpace(identity.ModuleId))
            {
                envVars.Add(new EnvVar { Key = Constants.ModuleIdVariableName, Value = identity.ModuleId });
            }

            if (!envVars.Exists(e => e.Key == Logger.RuntimeLogLevelEnvKey))
            {
                envVars.Add(new EnvVar { Key = Logger.RuntimeLogLevelEnvKey, Value = Logger.GetLogLevel().ToString() });
            }

            configSource.Configuration.GetValue<string>(Constants.UpstreamProtocolKey).ToUpstreamProtocol().ForEach(
                u =>
                {
                    if (!envVars.Any(e => e.Key.Equals(Constants.UpstreamProtocolKey, StringComparison.OrdinalIgnoreCase)))
                    {
                        envVars.Add(new EnvVar { Key = Constants.UpstreamProtocolKey, Value = u.ToString() });
                    }
                });

            if (identity.ModuleId.Equals(Constants.EdgeAgentModuleIdentityName))
            {
                string managementUri = configSource.Configuration.GetValue<string>(Constants.EdgeletManagementUriVariableName);
                if (!string.IsNullOrEmpty(managementUri))
                {
                    envVars.Add(new EnvVar { Key = Constants.EdgeletManagementUriVariableName, Value = managementUri });
                }

                string networkId = configSource.Configuration.GetValue<string>(Constants.NetworkIdKey);
                if (!string.IsNullOrEmpty(networkId))
                {
                    envVars.Add(new EnvVar { Key = Constants.NetworkIdKey, Value = networkId });
                }

                envVars.Add(new EnvVar { Key = Constants.ModeKey, Value = Constants.IotedgedMode });
            }

            // Set the edgelet's api version
            string apiVersion = configSource.Configuration.GetValue<string>(Constants.EdgeletApiVersionVariableName);
            if (!string.IsNullOrEmpty(apiVersion))
            {
                envVars.Add(new EnvVar { Key = Constants.EdgeletApiVersionVariableName, Value = apiVersion });
            }

            return envVars;
        }

        enum Operation
        {
            Create,
            Update,
            UpdateAndStart
        }
    }
}
