// Copyright (c) Microsoft. All rights reserved.

using System;

namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter.Interfaces
{
    public interface IConnectionStatusListener
    {
        public void onConnected(IMqttClient mqttClient);
        public void onDisconnected(IMqttClient mqttClient, Exception exception);
    }
}
