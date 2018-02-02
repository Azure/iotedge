// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    using System.Collections.Generic;
    using Microsoft.Azure.Amqp;
    using Microsoft.Azure.Amqp.Transport;

    class AmqpTransportListenerProvider : ITransportListenerProvider
    {
        public TransportListener Create(IEnumerable<TransportListener> listeners, AmqpSettings amqpSettings) => new AmqpTransportListener(listeners, amqpSettings);
    }
}
