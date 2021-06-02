// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    public class MqttBridgeEventIds
    {
        const int EventIdStart = 7300;
        public const int MqttBridgeProtocolHead = EventIdStart;
        public const int MqttBridgeConnector = EventIdStart + 50;
        public const int ConnectionHandler = EventIdStart + 100;
        public const int DeviceProxy = EventIdStart + 150;
        public const int TwinHandler = EventIdStart + 200;
        public const int TelemetryHandler = EventIdStart + 250;
        public const int Cloud2DeviceMessageHandler = EventIdStart + 300;
        public const int DirectMethodHandler = EventIdStart + 350;
        public const int ModuleToModuleMessageHandler = EventIdStart + 400;
        public const int SubscriptionChangeHandler = EventIdStart + 450;
        public const int MessageConfirmingHandler = EventIdStart + 500;
        public const int BrokeredCloudProxyDispatcher = EventIdStart + 550;
        public const int BrokeredCloudConnection = EventIdStart + 600;
    }
}
