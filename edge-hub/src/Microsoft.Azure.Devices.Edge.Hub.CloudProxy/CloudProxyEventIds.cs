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
        public const int DeviceConnectivityManager = EventIdStart + 400;
        public const int TokenCredentialsAuthenticator = EventIdStart + 500;
        public const int ConnectivityAwareClient = EventIdStart + 600;
    }
}
