// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;

    public class EnvironmentDeviceClientProvider : IDeviceClientProvider
    {
        readonly Option<UpstreamProtocol> upstreamProtocol;

        public EnvironmentDeviceClientProvider(Option<UpstreamProtocol> upstreamProtocol)
        {
            this.upstreamProtocol = upstreamProtocol;
        }

        public Task<IDeviceClient> Create(
            ConnectionStatusChangesHandler statusChangedHandler,
            Func<IDeviceClient, Task> initialize) =>
            DeviceClient.Create(this.upstreamProtocol, statusChangedHandler, initialize);
    }
}
