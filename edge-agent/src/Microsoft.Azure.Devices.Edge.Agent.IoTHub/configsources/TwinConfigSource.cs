// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.ConfigSources
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class TwinConfigSource : IConfigSource
    {
        readonly IEdgeAgentConnection edgeAgentConnection;

        public TwinConfigSource(IEdgeAgentConnection edgeAgentConnection, IAgentAppSettings appSettings)
        {
            this.AppSettings = Preconditions.CheckNotNull(appSettings, nameof(appSettings));
            this.edgeAgentConnection = Preconditions.CheckNotNull(edgeAgentConnection, nameof(edgeAgentConnection));
            Events.Created();
        }

        public IAgentAppSettings AppSettings { get; }

        public async Task<DeploymentConfigInfo> GetDeploymentConfigInfoAsync()
        {
            Option<DeploymentConfigInfo> deploymentConfig = await this.edgeAgentConnection.GetDeploymentConfigInfoAsync();
            return deploymentConfig.GetOrElse(DeploymentConfigInfo.Empty);
        }

        public void Dispose() => this.edgeAgentConnection.Dispose();

        static class Events
        {
            const int IdStart = AgentEventIds.TwinConfigSource;
            static readonly ILogger Log = Logger.Factory.CreateLogger<TwinConfigSource>();

            enum EventIds
            {
                Created = IdStart
            }

            public static void Created() => Log.LogDebug((int)EventIds.Created, "TwinConfigSource Created");
        }
    }
}
