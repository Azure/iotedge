// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    public static class CloudProxyEventIds
    {
        const int EventIdStart = 3000;
        public const int CloudProxy = EventIdStart;
        public const int CloudReceiver = EventIdStart + 100;
        public const int CloudConnectionProvider = EventIdStart + 200;
        public const int CloudConnection = EventIdStart + 300;
    }
}
