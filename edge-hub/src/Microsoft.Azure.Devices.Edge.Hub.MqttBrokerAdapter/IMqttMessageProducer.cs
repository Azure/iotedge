// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    public interface IMqttMessageProducer
    {
        void SetConnector(IMqttBridgeConnector connector);
    }
}
