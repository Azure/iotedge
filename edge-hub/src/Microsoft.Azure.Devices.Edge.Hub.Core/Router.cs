// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using Microsoft.Azure.Devices.Edge.Util;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public class Router : IRouter
    {
        readonly IDispatcher dispatcher;

        public Router(IDispatcher dispatcher)
        {
            this.dispatcher = Preconditions.CheckNotNull(dispatcher, nameof(dispatcher));
        }

        public Task RouteMessage(IMessage message, string deviceId)
        {
            return this.dispatcher.Dispatch(message, new HashSet<Endpoint> { new Endpoint(EndpointType.Cloud, deviceId) });
        }

        public async Task RouteMessageBatch(IEnumerable<IMessage> messages, string deviceId)
        {
            foreach (IMessage message in messages)
            {
                await this.RouteMessage(message, deviceId);
            }            
        }
    }
}
