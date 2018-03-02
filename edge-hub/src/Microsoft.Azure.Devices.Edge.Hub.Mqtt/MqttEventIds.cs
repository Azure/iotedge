// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    public static class MqttEventIds
    {
        const int EventIdStart = 4000;
        public const int SasTokenDeviceIdentityProvider = EventIdStart;
        public const int DeviceProxy = EventIdStart + 100;
        public const int MessagingServiceClient = EventIdStart + 200;
        public const int SessionStatePersistenceProvider = EventIdStart + 300;
        public const int SessionStateStoragePersistenceProvider = EventIdStart + 400;
        public const int MqttWebSocketListener = EventIdStart + 500;
        public const int ServerWebSocketChannel = EventIdStart + 600;
    }
}
