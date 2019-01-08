// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    using Microsoft.Azure.Amqp;
    using Microsoft.Azure.Devices.Edge.Util;

    /// <summary>
    /// This class wraps an AmqpSession, and provides similar functionality.
    /// This allows unit testing the components that use it
    /// </summary>
    public class EdgeAmqpSession : IAmqpSession
    {
        public EdgeAmqpSession(AmqpSession amqpSession)
        {
            Preconditions.CheckNotNull(amqpSession, nameof(amqpSession));
            this.Connection = new EdgeAmqpConnection(amqpSession.Connection);
        }

        public IAmqpConnection Connection { get; }
    }
}
