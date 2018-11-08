// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    public static class AmqpEventIds
    {
        public const int SaslPlainAuthenticator = EventIdStart;
        public const int AmqpProtocolHead = EventIdStart + 100;
        public const int CbsLinkHandler = EventIdStart + 200;
        public const int CbsNode = EventIdStart + 300;
        public const int ConnectionHandler = EventIdStart + 400;
        public const int ReceivingLinkHandler = EventIdStart + 500;
        public const int EventsLinkHandler = EventIdStart + 550;
        public const int TwinReceivingLinkHandler = EventIdStart + 570;
        public const int SendingLinkHandler = EventIdStart + 600;
        public const int DeviceBoundLinkHandler = EventIdStart + 650;
        public const int LinkHandler = EventIdStart + 700;
        public const int AmqpWebSocketListener = EventIdStart + 800;
        public const int ServerWebSocketTransport = EventIdStart + 900;
        const int EventIdStart = 5000;
    }
}
