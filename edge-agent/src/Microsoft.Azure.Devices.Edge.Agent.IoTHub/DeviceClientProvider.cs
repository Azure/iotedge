// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;

    public class DeviceClientProvider : IDeviceClientProvider
    {
        readonly string edgeAgentConnectionString;
        readonly Option<UpstreamProtocol> upstreamProtocol;

        public DeviceClientProvider(EdgeHubConnectionString connectionStringBuilder, Option<UpstreamProtocol> upstreamProtocol)
        {
            this.edgeAgentConnectionString = ConstructModuleConnectionString(Preconditions.CheckNotNull(connectionStringBuilder, nameof(connectionStringBuilder)));
            this.upstreamProtocol = upstreamProtocol;
        }

        static string ConstructModuleConnectionString(EdgeHubConnectionString connectionDetails)
        {
            EdgeHubConnectionString agentConnectionString = new EdgeHubConnectionString.EdgeHubConnectionStringBuilder(connectionDetails.HostName, connectionDetails.DeviceId)
                .SetSharedAccessKey(connectionDetails.SharedAccessKey)
                .SetModuleId(Constants.EdgeAgentModuleIdentityName)
                .Build();
            return agentConnectionString.ToConnectionString();
        }

        public Task<IDeviceClient> Create(
            ConnectionStatusChangesHandler statusChangedHandler,
            Func<IDeviceClient, Task> initialize) =>
            DeviceClient.Create(this.edgeAgentConnectionString, this.upstreamProtocol, statusChangedHandler, initialize);
    }
}
