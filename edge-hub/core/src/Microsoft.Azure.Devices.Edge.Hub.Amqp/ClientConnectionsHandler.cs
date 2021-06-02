// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    using System.Collections.Concurrent;
    using Microsoft.Azure.Amqp;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;

    class ClientConnectionsHandler : IClientConnectionsHandler
    {
        readonly ConcurrentDictionary<string, ClientConnectionHandler> connectionHandlers = new ConcurrentDictionary<string, ClientConnectionHandler>();
        readonly IConnectionProvider connectionProvider;
        readonly AmqpConnection amqpConnection;

        public ClientConnectionsHandler(IConnectionProvider connectionProvider, AmqpConnection amqpConnection)
        {
            this.connectionProvider = Preconditions.CheckNotNull(connectionProvider, nameof(connectionProvider));
            this.amqpConnection = Preconditions.CheckNotNull(amqpConnection, nameof(amqpConnection));
        }

        public IConnectionHandler GetConnectionHandler(IIdentity identity) =>
            this.connectionHandlers.GetOrAdd(identity.Id, i => new ClientConnectionHandler(identity, this.connectionProvider, this.amqpConnection));
    }
}
