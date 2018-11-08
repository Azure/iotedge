// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    using System.Collections.Generic;

    using Microsoft.Azure.Amqp;
    using Microsoft.Azure.Amqp.Transport;

    public class AmqpTransportListenerProvider : ITransportListenerProvider
    {
        public TransportListener Create(IEnumerable<TransportListener> listeners, AmqpSettings amqpSettings) => new AmqpTransportListener(listeners, amqpSettings);
    }
}
