// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;

    public class EnvironmentModuleClientProvider : IModuleClientProvider
    {
        readonly Option<UpstreamProtocol> upstreamProtocol;

        public EnvironmentModuleClientProvider(Option<UpstreamProtocol> upstreamProtocol)
        {
            this.upstreamProtocol = upstreamProtocol;
        }

        public Task<IModuleClient> Create(
            ConnectionStatusChangesHandler statusChangedHandler,
            Func<IModuleClient, Task> initialize) =>
            ModuleClient.Create(this.upstreamProtocol, statusChangedHandler, initialize);
    }
}
