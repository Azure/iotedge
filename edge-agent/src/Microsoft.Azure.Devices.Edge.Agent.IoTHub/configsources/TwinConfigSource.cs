// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.ConfigSources
{
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

        public async Task<DeploymentConfigInfo> GetDeploymentConfigInfoAsync()
        {
            Option<DeploymentConfigInfo> deploymentConfig = await this.edgeAgentConnection.GetDeploymentConfigInfoAsync();
            return deploymentConfig.GetOrElse(DeploymentConfigInfo.Empty);
        }

        public void Dispose() => this.edgeAgentConnection.Dispose();

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<TwinConfigSource>();
            const int IdStart = AgentEventIds.TwinConfigSource;

            enum EventIds
            {
                Created = IdStart
            }

            public static void Created() => Log.LogDebug((int)EventIds.Created, "TwinConfigSource Created");
        }
    }
}
