// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    public class AuthAgentProtocolHeadConfig
    {
        public AuthAgentProtocolHeadConfig(int port, string baseUrl)
        {
            this.Port = port;
            this.BaseUrl = baseUrl;
        }

        public int Port { get; private set; }
        public string BaseUrl { get; private set; }
    }
}
