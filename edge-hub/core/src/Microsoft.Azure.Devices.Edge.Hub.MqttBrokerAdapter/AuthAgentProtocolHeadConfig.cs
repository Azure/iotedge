// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    using Microsoft.Azure.Devices.Edge.Util;

    public class AuthAgentProtocolHeadConfig
    {
        public AuthAgentProtocolHeadConfig(int port, string baseUrl)
        {
            this.Port = Preconditions.CheckNotNull(port, nameof(port));
            this.BaseUrl = Preconditions.CheckNotNull(baseUrl, nameof(baseUrl));
        }

        public int Port { get; }
        public string BaseUrl { get; }
    }
}
