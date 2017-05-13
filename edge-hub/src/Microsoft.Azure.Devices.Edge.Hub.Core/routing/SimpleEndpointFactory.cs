// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Routing
{
    using System;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Routing.Core;
    using IRoutingMessage = Microsoft.Azure.Devices.Routing.Core.IMessage;

    public class SimpleEndpointFactory : IEndpointFactory
    {
        readonly IConnectionManager connectionManager;
        readonly Core.IMessageConverter<IRoutingMessage> messageConverter;

        public SimpleEndpointFactory(IConnectionManager connectionManager,
            Core.IMessageConverter<IRoutingMessage> messageConverter)
        {
            this.connectionManager = Preconditions.CheckNotNull(connectionManager);
            this.messageConverter = Preconditions.CheckNotNull(messageConverter);
        }

        // Always returns a cloud endpoint without any parsing for now.
        public Endpoint Create(string endpoint)
        {
            return new CloudEndpoint(Guid.NewGuid().ToString(), (id) => this.connectionManager.GetCloudConnection(id), this.messageConverter);
        }
    }
}