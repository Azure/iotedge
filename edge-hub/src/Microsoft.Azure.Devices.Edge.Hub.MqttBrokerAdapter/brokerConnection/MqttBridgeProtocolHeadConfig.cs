// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    public class MqttBridgeProtocolHeadConfig
    {
        public MqttBridgeProtocolHeadConfig(int port, string url)
        {
            this.Port = port;
            this.Url = url;
        }

        public int Port { get; private set; }
        public string Url { get; private set; }
    }
}
