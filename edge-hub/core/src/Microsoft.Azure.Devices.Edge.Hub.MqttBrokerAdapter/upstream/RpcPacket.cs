// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    public class RpcPacket
    {
        public string Version { get; set; }
        public string Cmd { get; set; }
        public string Topic { get; set; }
        public byte[] Payload { get; set; }
    }
}
