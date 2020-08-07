// Copyright (c) Microsoft. All rights reserved.
using System;

namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter.Interfaces
{
    public interface IMqttClientProvider
    {
        public IMqttClient CreateMqttClient(string host, int port, bool isSsl);
    }
}
