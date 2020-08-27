// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    using System;
    using System.Threading.Tasks;

    public interface IMqttBrokerConnector
    {
        Task ConnectAsync(string serverAddress, int port);
        Task DisconnectAsync();

        Task<bool> SendAsync(string topic, byte[] payload);

        public event EventHandler OnConnected;
    }
}
