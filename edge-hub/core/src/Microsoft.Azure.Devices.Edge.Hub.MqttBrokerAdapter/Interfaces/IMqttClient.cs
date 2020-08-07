// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter.Interfaces
{
    public interface IMqttClient
    {
        public Task ConnectAsync(string clientId, string username, string password, CancellationToken cancellationToken);
        public Task ConnectAsync(string clientId, string username, string password, bool cleanSession, TimeSpan keepAlivePeriod, CancellationToken cancellationToken);
        public Task DisconnectAsync(CancellationToken cancellationToken);
        public Task PublishAsync(string topic, byte[] payload, Qos qos, CancellationToken cancellationToken);
        public Task<Dictionary<string, Qos>> SubscribeAsync(Dictionary<string, Qos> subscriptions, CancellationToken cancellationToken);
        public void RegisterMessageHandler(IMessageHandler messageHandler);
        public Task UnsubscribeAsync(IEnumerable<string> topics, CancellationToken cancellationToken);
        public void RegisterConnectionStatusListener(IConnectionStatusListener connectionStatusListener);
        public bool IsConnected();
    }
}
