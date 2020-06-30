// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    public interface IMessageProducer
    {
        void SetConnector(IMqttBrokerConnector connector);
    }
}
