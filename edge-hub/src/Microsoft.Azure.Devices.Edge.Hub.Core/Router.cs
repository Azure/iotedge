// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using Microsoft.Azure.Devices.Edge.Util;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    class Router : IRouter
    {
        readonly IDispatcher dispatcher;

        public Router(IDispatcher dispatcher)
        {
            this.dispatcher = Preconditions.CheckNotNull(dispatcher);
        }

        public async Task RouteMessage(IMessage message)
        {
            await this.dispatcher.Dispatch(message, new HashSet<Endpoint> { new Endpoint(EndpointType.Cloud, string.Empty) });
        }
    }
}
