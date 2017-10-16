// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.ConfigSources
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    public class TwinConfigSource : IConfigSource
    {
        readonly IEdgeAgentConnection edgeAgentConnection;

        public TwinConfigSource(IEdgeAgentConnection edgeAgentConnection, IConfiguration configuration)
        {
            this.Configuration = Preconditions.CheckNotNull(configuration, nameof(configuration));
            this.edgeAgentConnection = Preconditions.CheckNotNull(edgeAgentConnection, nameof(edgeAgentConnection));
            Events.Created();
        }

        public IConfiguration Configuration { get; }

        public async Task<AgentConfig> GetAgentConfigAsync()
        {
            Option<DeploymentConfigInfo> deploymentConfig = await this.edgeAgentConnection.GetDeploymentConfigInfoAsync();
            AgentConfig agentConfig = deploymentConfig
                .Map(d => GetAgentConfig(d))
                .GetOrElse(AgentConfig.Empty);
            return agentConfig;
        }

        static AgentConfig GetAgentConfig(DeploymentConfigInfo deploymentConfigInfo)
        {
            try
            {
                DeploymentConfig deploymentConfig = deploymentConfigInfo.DeploymentConfig;
                var modules = new Dictionary<string, IModule>();
                foreach (KeyValuePair<string, IModule> module in deploymentConfig.Modules)
                {
                    module.Value.Name = module.Key;
                    modules.Add(module.Key, module.Value);
                }

                if (deploymentConfig.SystemModules.EdgeHub != null)
                {
                    modules.Add(deploymentConfig.SystemModules.EdgeHub.Name, deploymentConfig.SystemModules.EdgeHub);
                }

                var moduleSet = new ModuleSet(modules);
                var agentConfig = new AgentConfig(
                    deploymentConfigInfo.DesiredPropertiesVersion,
                    deploymentConfig.Runtime,
                    moduleSet,
                    Option.Some(deploymentConfig.SystemModules.EdgeAgent)
                );
                return agentConfig;
            }
            catch (Exception ex)
            {
                Events.ConversionError(ex);
                return AgentConfig.Empty;
            }
        }

        public void Dispose() => this.edgeAgentConnection.Dispose();

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<TwinConfigSource>();
            const int IdStart = AgentEventIds.TwinConfigSource;

            enum EventIds
            {
                Created = IdStart,
                AgentConfigConversionError
            }

            public static void Created() => Log.LogDebug((int)EventIds.Created, "TwinConfigSource Created");

            public static void ConversionError(Exception ex) => Log.LogError((int)EventIds.AgentConfigConversionError, ex, "Error getting Edge Agent config from deployment config");
        }
    }
}
