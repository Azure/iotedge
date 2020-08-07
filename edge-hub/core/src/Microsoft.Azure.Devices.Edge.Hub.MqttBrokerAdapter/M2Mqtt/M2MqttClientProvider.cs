// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter.Interfaces;
using uPLibrary.Networking.M2Mqtt;

namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter.M2Mqtt
{
    public class M2MqttClientProvider : IMqttClientProvider
    {
        public IMqttClient CreateMqttClient(string host, int port, bool isSsl) => new M2MqttClient(host, port, isSsl);
    }
}
