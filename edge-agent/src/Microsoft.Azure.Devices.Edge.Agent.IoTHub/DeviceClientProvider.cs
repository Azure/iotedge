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

        public DeviceClientProvider(string deviceConnectionString, Option<UpstreamProtocol> upstreamProtocol)
        {
            this.edgeAgentConnectionString = ConstructModuleConnectionString(Preconditions.CheckNonWhiteSpace(deviceConnectionString, nameof(deviceConnectionString)));
            this.upstreamProtocol = upstreamProtocol;
        }

        static string ConstructModuleConnectionString(string deviceConnectionString) =>
            $"{deviceConnectionString};{Constants.ModuleIdKey}={Constants.EdgeAgentModuleIdentityName}";

        public Task<IDeviceClient> Create(
            ConnectionStatusChangesHandler statusChangedHandler,
            Func<IDeviceClient, Task> initialize) =>
            DeviceClient.Create(this.edgeAgentConnectionString, this.upstreamProtocol, statusChangedHandler, initialize);
    }
}
