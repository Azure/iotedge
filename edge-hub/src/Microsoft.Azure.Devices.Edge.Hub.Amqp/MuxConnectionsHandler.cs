// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    using System.Collections.Concurrent;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Util;

    class MuxConnectionsHandler
    {
        readonly ConcurrentDictionary<string, ConnectionHandler> connectionHandlers = new ConcurrentDictionary<string, ConnectionHandler>();
        readonly IAmqpConnection connection;
        readonly IConnectionProvider connectionProvider;

        public MuxConnectionsHandler(IAmqpConnection connection, IConnectionProvider connectionProvider)
        {
            this.connection = Preconditions.CheckNotNull(connection, nameof(connection));
            this.connectionProvider = Preconditions.CheckNotNull(connectionProvider, nameof(connectionProvider));
        }

        public ConnectionHandler GetConnectionHandler(string id) => this.connectionHandlers.GetOrAdd(id, i => new ConnectionHandler(this.connection, this.connectionProvider));
    }
}
