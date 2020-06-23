// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;

    public interface IClientConnectionsHandler
    {
        IConnectionHandler GetConnectionHandler(IIdentity identity);
    }
}
