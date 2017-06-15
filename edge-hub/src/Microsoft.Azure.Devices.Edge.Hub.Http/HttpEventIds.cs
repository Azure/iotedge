// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http
{
    public class HttpEventIds
    {
        const int EventIdStart = 6000;
        public const int AuthenticationMiddleware = EventIdStart;
        public const int ExceptionFilter = EventIdStart + 100;
        public const int TwinsController = EventIdStart + 100;
    }
}