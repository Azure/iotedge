// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub
{
    using System;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;

    public class ModuleClientProvider : IModuleClientProvider
    {
        readonly string edgeAgentConnectionString;
        readonly Option<UpstreamProtocol> upstreamProtocol;
        readonly Option<IWebProxy> proxy;
        readonly Option<string> productInfo;

        public ModuleClientProvider(string edgeAgentConnectionString, Option<UpstreamProtocol> upstreamProtocol, Option<IWebProxy> proxy, Option<string> productInfo)
        {
            this.edgeAgentConnectionString = Preconditions.CheckNonWhiteSpace(edgeAgentConnectionString, nameof(edgeAgentConnectionString));
            this.upstreamProtocol = upstreamProtocol;
            this.proxy = proxy;
            this.productInfo = productInfo;
        }

        public Task<IModuleClient> Create(
            ConnectionStatusChangesHandler statusChangedHandler,
            Func<IModuleClient, Task> initialize) =>
            ModuleClient.Create(Option.Some(this.edgeAgentConnectionString), this.upstreamProtocol, statusChangedHandler, initialize, this.proxy, this.productInfo);
    }
}
