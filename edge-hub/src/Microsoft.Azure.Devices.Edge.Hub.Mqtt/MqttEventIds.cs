// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    public static class MqttEventIds
    {
        const int EventIdStart = 4000;
        public const int SasTokenDeviceIdentityProvider = EventIdStart;
        public const int DeviceProxy = EventIdStart + 100;
        public const int MessagingServiceClient = EventIdStart + 200;
    }
}