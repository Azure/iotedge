// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    public class AuthAgentEventIds
    {
        const int EventIdStart = 7000;
        public const int AuthAgentProtocolHead = EventIdStart;
        public const int AuthAgentController = EventIdStart + 200;
    }
}
