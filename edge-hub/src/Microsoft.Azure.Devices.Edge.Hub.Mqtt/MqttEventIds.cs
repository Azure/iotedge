// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    public static class MqttEventIds
    {
        public const int SasTokenDeviceIdentityProvider = EventIdStart;
        public const int DeviceProxy = EventIdStart + 100;
        public const int MessagingServiceClient = EventIdStart + 200;
        public const int SessionStatePersistenceProvider = EventIdStart + 300;
        public const int SessionStateStoragePersistenceProvider = EventIdStart + 400;
        public const int MqttWebSocketListener = EventIdStart + 500;
        public const int ServerWebSocketChannel = EventIdStart + 600;
        const int EventIdStart = 4000;
    }
}
