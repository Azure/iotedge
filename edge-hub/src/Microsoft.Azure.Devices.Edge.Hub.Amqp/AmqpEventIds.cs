// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    public static class AmqpEventIds
    {
        const int EventIdStart = 5000;
        public const int SaslPlainAuthenticator = EventIdStart;
        public const int AmqpProtocolHead = EventIdStart + 100;
        public const int CbsLinkHandler = EventIdStart + 200;
        public const int EventsLinkHandler = EventIdStart + 300;
        public const int CbsNode = EventIdStart + 400;
        public const int ConnectionHandler = EventIdStart + 500;
        public const int DeviceBoundLinkHandler = EventIdStart + 600;
    }
}
