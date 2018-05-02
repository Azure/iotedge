// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Edgelet.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Edgelet.GeneratedCode;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;
    using Microsoft.Extensions.Configuration;
    using System.Collections.ObjectModel;

    public class CreateOrUpdateCommand : ICommand
    {
        readonly IModuleManager moduleManager;
        readonly ModuleSpec moduleSpec;
        readonly Lazy<string> id;
        readonly bool isUpdate;

        public CreateOrUpdateCommand(IModuleManager moduleManager, ModuleSpec moduleSpec, bool isUpdate)
        {
            this.moduleManager = Preconditions.CheckNotNull(moduleManager, nameof(moduleManager));
            this.moduleSpec = Preconditions.CheckNotNull(moduleSpec, nameof(moduleSpec));
            this.id = new Lazy<string>(() => JsonConvert.SerializeObject(this.moduleSpec).CreateSha256());
            this.isUpdate = isUpdate;
        }

        public static CreateOrUpdateCommand Build(IModuleManager moduleManager, IModule module, IModuleIdentity identity,
            IConfigSource configSource, object settings, bool isEdgeHub, bool isUpdate)
        {
            Preconditions.CheckNotNull(moduleManager, nameof(moduleManager));
            Preconditions.CheckNotNull(module, nameof(module));
            Preconditions.CheckNotNull(identity, nameof(identity));
            Preconditions.CheckNotNull(configSource, nameof(configSource));
            Preconditions.CheckNotNull(settings, nameof(settings));

            IEnumerable<EnvVar> envVars = GetEnvVars(identity, configSource, isEdgeHub);
            ModuleSpec moduleSpec = BuildModuleSpec(module, envVars, settings);
            return new CreateOrUpdateCommand(moduleManager, moduleSpec, isUpdate);
        }

        public string Id => this.id.Value;

        public Task ExecuteAsync(CancellationToken token) => !this.isUpdate
            ? this.moduleManager.CreateModuleAsync(moduleSpec)
            : this.moduleManager.UpdateModuleAsync(moduleSpec);

        public string Show() => !this.isUpdate
            ? $"Create module {this.moduleSpec.Name}"
            : $"Update module {this.moduleSpec.Name}";

        public Task UndoAsync(CancellationToken token) => TaskEx.Done;

        static ModuleSpec BuildModuleSpec(IModule module, IEnumerable<EnvVar> envVars, object settings)
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

        static IEnumerable<EnvVar> GetEnvVars(IModuleIdentity identity, IConfigSource configSource, bool isEdgeHub)
        {
            var envVars = new List<EnvVar>();

            // Inject the connection details as an environment variable
            if (identity.Credentials is IdentityProviderServiceCredentials creds)
            {
                if (!string.IsNullOrWhiteSpace(creds.ProviderUri))
                {
                    envVars.Add(new EnvVar { Key = Constants.EdgeletUriVariableName, Value = creds.ProviderUri });
                }

                creds.Version.ForEach(v => envVars.Add(new EnvVar { Key = Constants.EdgeletVersionVariableName, Value = v }));

                if (!string.IsNullOrWhiteSpace(creds.AuthScheme))
                {
                    envVars.Add(new EnvVar { Key = Constants.EdgeletAuthSchemeVariableName, Value = creds.AuthScheme });
                }
            }

            if (!string.IsNullOrWhiteSpace(identity.IotHubHostname))
            {
                envVars.Add(new EnvVar { Key = Constants.IotHubHostnameVariableName, Value = identity.IotHubHostname });
            }

            if (!string.IsNullOrWhiteSpace(identity.GatewayHostname) && !isEdgeHub)
            {
                envVars.Add(new EnvVar { Key = Constants.GatewayHostnameVariableName, Value = identity.GatewayHostname });
            }

            if (!string.IsNullOrWhiteSpace(identity.DeviceId))
            {
                envVars.Add(new EnvVar { Key = Constants.DeviceIdVariableName, Value = identity.DeviceId });
            }

            if (!string.IsNullOrWhiteSpace(identity.ModuleId))
            {
                envVars.Add(new EnvVar { Key = Constants.ModuleIdVariableName, Value = identity.ModuleId });
            }

            envVars.Add(new EnvVar { Key = Logger.RuntimeLogLevelEnvKey, Value = Logger.GetLogLevel().ToString() });

            configSource.Configuration.GetValue<string>(Constants.UpstreamProtocolKey).ToUpstreamProtocol().ForEach(
                u =>
                {
                    if (!envVars.Any(e => e.Key.Equals(Constants.UpstreamProtocolKey, StringComparison.OrdinalIgnoreCase)))
                    {
                        envVars.Add(new EnvVar { Key = Constants.UpstreamProtocolKey, Value = u.ToString() });
                    }
                });

            return envVars;
        }
    }
}
