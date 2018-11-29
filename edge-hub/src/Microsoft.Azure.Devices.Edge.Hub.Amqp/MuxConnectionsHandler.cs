// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    using System.Collections.Concurrent;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;

    class MuxConnectionsHandler : IAmqpClientConnectionsHandler
    {
        readonly ConcurrentDictionary<string, ConnectionHandler> connectionHandlers = new ConcurrentDictionary<string, ConnectionHandler>();
        readonly IConnectionProvider connectionProvider;

        public MuxConnectionsHandler(IConnectionProvider connectionProvider)
        {
            this.connectionProvider = Preconditions.CheckNotNull(connectionProvider, nameof(connectionProvider));
        }

        public IConnectionHandler GetConnectionHandler(IIdentity identity) =>
            this.connectionHandlers.GetOrAdd(identity.Id, i => new ConnectionHandler(identity, this.connectionProvider));
    }

    public interface IAmqpClientConnectionsHandler
    {
        IConnectionHandler GetConnectionHandler(IIdentity identity);
    }
}
