// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test
{
    using Microsoft.Azure.Devices.Client;

    public static class TransportHelper
    {
        public static readonly ITransportSettings[] AmqpTcpTransportSettings =
            {
                new AmqpTransportSettings(TransportType.Amqp_Tcp_Only)
                {
                    AmqpConnectionPoolSettings = new AmqpConnectionPoolSettings()
                    {
                        Pooling = true,
                        MaxPoolSize = 1
                    }
                }
            };
    }
}