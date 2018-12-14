// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http
{
    public class HttpEventIds
    {
        const int EventIdStart = 6000;
        public const int AuthenticationMiddleware = EventIdStart;
        public const int ExceptionFilter = EventIdStart + 100;
        public const int TwinsController = EventIdStart + 200;
        public const int HttpProtocolHead = EventIdStart + 300;
        public const int WebSocketListenerRegistry = EventIdStart + 400;
        public const int WebSocketHandlingMiddleware = EventIdStart + 500;
        public const int HttpsExtensionConnectionAdapter = EventIdStart + 600;
    }
}
