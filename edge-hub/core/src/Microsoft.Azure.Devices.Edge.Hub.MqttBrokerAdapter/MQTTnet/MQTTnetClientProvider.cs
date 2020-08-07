// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter.Interfaces;
using MQTTnet;
using System;

namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter.MQTTnet
{
    public class MQTTnetClientProvider : IMqttClientProvider
    {
        public IMqttClient CreateMqttClient(string host, int port, bool isSsl) => new MQTTnetClient(host, port, isSsl);
    }
}
