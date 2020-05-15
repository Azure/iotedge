// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    public class MqttPublishInfo
    {
        public MqttPublishInfo(string topic, byte[] payload)
        {
            this.Topic = topic;
            this.Payload = payload;
        }

        public string Topic { get; }
        public byte[] Payload { get; }
    }
}
