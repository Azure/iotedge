// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    public class MqttPublishInfo
    {
        public MqttPublishInfo(string topic, byte[] payload)
        {
            this.Topic = topic ?? string.Empty;
            this.Payload = payload ?? new byte[0];
        }

        public string Topic { get; }
        public byte[] Payload { get; }
    }
}
