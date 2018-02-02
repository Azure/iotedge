// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    public static class AmqpEventIds
    {
        const int EventIdStart = 5000;
        public const int SaslPlainAuthenticator = EventIdStart;
        public const int AmqpProtocolHead = EventIdStart + 100;
    }
}
