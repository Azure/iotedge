// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    using System;
    using Microsoft.Azure.Amqp;
    using Microsoft.Azure.Amqp.Framing;
    using Microsoft.Azure.Amqp.Transport;

    public class AmqpRuntimeProvider : IRuntimeProvider
    {
        AmqpSettings amqpSettings;

        AmqpConnection IConnectionFactory.CreateConnection(
            TransportBase transport,
            ProtocolHeader protocolHeader,
            bool isInitiator,
            AmqpSettings settings,
            AmqpConnectionSettings connectionSettings)
        {
            this.amqpSettings = settings;

            if (settings.RequireSecureTransport && !transport.IsSecure)
            {
                throw new AmqpException(AmqpErrorCode.NotAllowed, "AMQP transport is not secure");
            }

            var connection = new AmqpConnection(transport, protocolHeader, false, settings, connectionSettings)
            {
                SessionFactory = this
            };

            return connection;
        }

        AmqpSession ISessionFactory.CreateSession(AmqpConnection connection, AmqpSessionSettings settings)
        {
            return new AmqpSession(connection, settings, this.amqpSettings.RuntimeProvider);
        }

        IAsyncResult ILinkFactory.BeginOpenLink(AmqpLink link, TimeSpan timeout, AsyncCallback callback, object state)
        {
            throw new NotImplementedException();
        }

        AmqpLink ILinkFactory.CreateLink(AmqpSession session, AmqpLinkSettings settings)
        {
            throw new NotImplementedException();
        }

        void ILinkFactory.EndOpenLink(IAsyncResult result)
        {
            throw new NotImplementedException();
        }
    }
}
