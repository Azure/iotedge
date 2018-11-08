// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
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
