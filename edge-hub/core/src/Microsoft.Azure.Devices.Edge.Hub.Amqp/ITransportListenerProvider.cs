// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    using System.Collections.Generic;
    using Microsoft.Azure.Amqp;
    using Microsoft.Azure.Amqp.Transport;

    public interface ITransportListenerProvider
    {
        TransportListener Create(IEnumerable<TransportListener> listeners, AmqpSettings amqpSettings);
    }
}
