// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    public static class CloudProxyEventIds
    {
        public const int CloudProxy = EventIdStart;
        public const int CloudReceiver = EventIdStart + 100;
        public const int CloudConnectionProvider = EventIdStart + 200;
        public const int CloudConnection = EventIdStart + 300;
        public const int DeviceConnectivityManager = EventIdStart + 400;
        public const int TokenCredentialsAuthenticator = EventIdStart + 500;
        public const int ConnectivityAwareClient = EventIdStart + 600;
        public const int ServiceProxy = EventIdStart + 700;
        public const int DeviceScopeApiClient = EventIdStart + 800;
        public const int CloudTokenAuthenticator = EventIdStart + 900;
        public const int CertificateCredentialsAuthenticator = EventIdStart + 1000;
        const int EventIdStart = 3000;
    }
}
