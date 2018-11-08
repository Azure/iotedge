// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Http
{
    public class HttpEventIds
    {
        public const int AuthenticationMiddleware = EventIdStart;
        public const int ExceptionFilter = EventIdStart + 100;
        public const int TwinsController = EventIdStart + 200;
        public const int HttpProtocolHead = EventIdStart + 300;
        public const int WebSocketListenerRegistry = EventIdStart + 400;
        public const int WebSocketHandlingMiddleware = EventIdStart + 500;
        const int EventIdStart = 6000;
    }
}
