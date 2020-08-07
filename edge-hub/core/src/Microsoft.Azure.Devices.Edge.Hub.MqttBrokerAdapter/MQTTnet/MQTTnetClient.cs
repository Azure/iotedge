// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter.Exceptions;
using Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter.Interfaces;
using Microsoft.Azure.Devices.Edge.Util;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Connecting;
using MQTTnet.Client.Disconnecting;
using MQTTnet.Client.Options;
using MQTTnet.Client.Publishing;
using MQTTnet.Client.Subscribing;
using MQTTnet.Client.Unsubscribing;
using MQTTnet.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter.MQTTnet
{
    public class MQTTnetClient : Interfaces.IMqttClient
    {
        private readonly string _host;
        private readonly int _port;
        private readonly bool _isSsl;
        private readonly global::MQTTnet.Client.IMqttClient _client;
        private readonly MQTTnetApplicationMessageReceivedHandler _mqttApplicationMessageReceivedHandler;
        private IConnectionStatusListener _connectionStatusListener;

        public MQTTnetClient(string host, int port, bool isSsl)
        {
            _host = Preconditions.CheckNotNull(host, nameof(host));
            _port = port;
            _isSsl = isSsl;
            _client = new MqttFactory().CreateMqttClient();
            _mqttApplicationMessageReceivedHandler = new MQTTnetApplicationMessageReceivedHandler();
        }

        public Task ConnectAsync(string clientId, string username, string password, CancellationToken cancellationToken) => ConnectAsync(clientId, username, password, true, default, cancellationToken);

        public async Task ConnectAsync(string clientId, string username, string password, bool cleanSession, TimeSpan keepAlivePeriod, CancellationToken cancellationToken)
        {
            var builder = new MqttClientOptionsBuilder()
                .WithTcpServer(_host, _port)
                .WithClientId(clientId)
                .WithCredentials(username, password)
                .WithCleanSession(cleanSession)
                .WithKeepAlivePeriod(keepAlivePeriod);
            // default is V311, supports V500
            // builder.WithProtocolVersion(MqttProtocolVersion.V500);
            if (_isSsl)
            {
                builder.WithTls();
            }

            _client.UseConnectedHandler(HandleConnection);
            _client.UseDisconnectedHandler(HandleDisconnection);
            _client.ApplicationMessageReceivedHandler = _mqttApplicationMessageReceivedHandler;

            MqttClientAuthenticateResult connectResult;
            try
            {
                connectResult = await _client.ConnectAsync(builder.Build(), cancellationToken);
            }
            catch(Exception e)
            {
                throw new MqttException(cause: e);
            }

            ValidateConnectResult(connectResult);
        }

        public Task DisconnectAsync(CancellationToken cancellationToken)
        {
            _client.UseConnectedHandler((Action<MqttClientConnectedEventArgs>)null);
            _client.UseDisconnectedHandler((Action<MqttClientDisconnectedEventArgs>)null);
            return _client.DisconnectAsync(new MqttClientDisconnectOptions(), cancellationToken);
        }

        public async Task PublishAsync(string topic, byte[] payload, Qos qos, CancellationToken cancellationToken)
        {
            Preconditions.CheckNonWhiteSpace(topic, nameof(topic));
            Preconditions.CheckNotNull(qos, nameof(qos));
            var mqttMessage = new MqttApplicationMessage()
            {
                Topic = topic,
                Payload = payload,
                QualityOfServiceLevel = MapQos(qos)
            };

            MqttClientPublishResult publishResult;
            try
            {
                publishResult = await _client.PublishAsync(mqttMessage, cancellationToken);
            }
            catch(Exception e)
            {
                throw new MqttException(cause: e);
            }

            ValidatePublishResult(publishResult);
        }

        public async Task<Dictionary<string, Qos>> SubscribeAsync(Dictionary<string, Qos> subscriptions, CancellationToken cancellationToken)
        {
            Preconditions.CheckNotNull(subscriptions, nameof(subscriptions));
            Preconditions.CheckArgument(subscriptions.Count > 0, "No subscriptions.");
            var builder = new MqttClientSubscribeOptionsBuilder();
            foreach (var entry in subscriptions)
            {
                builder.WithTopicFilter(entry.Key, MapQos(entry.Value));
            }

            try
            {
                var subscribeResult = await _client.SubscribeAsync(builder.Build(), cancellationToken);
                var gainedQoses = new Dictionary<string, Qos>();
                foreach (var item in subscribeResult.Items)
                {
                    var gained = (int)item.ResultCode;
                    if (gained < 0 || gained > 2)
                    {
                        throw new MqttException(message: $"Subscribe [topiic={item.TopicFilter.Topic}, qos={item.TopicFilter.QualityOfServiceLevel} failed: [resultCode={item.ResultCode}].");
                    }

                    gainedQoses[item.TopicFilter.Topic] = (Qos)gained;
                }
                return gainedQoses;
            }
            catch (Exception e)
            {
                throw new MqttException(cause: e);
            }
        }

        public Task UnsubscribeAsync(IEnumerable<string> topics, CancellationToken cancellationToken)
        {
            Preconditions.CheckNotNull(topics, nameof(topics));
            Preconditions.CheckArgument(topics.Count() > 0, "No topics.");
            var builder = new MqttClientUnsubscribeOptionsBuilder();
            foreach (string topic in topics)
            {
                builder.WithTopicFilter(topic);
            }
            return _client.UnsubscribeAsync(builder.Build(), cancellationToken);
        }

        public void RegisterConnectionStatusListener(IConnectionStatusListener connectionStatusListener) => _connectionStatusListener = connectionStatusListener;

        public void RegisterMessageHandler(IMessageHandler messageHandler) => _mqttApplicationMessageReceivedHandler.MessageHandler = messageHandler;

        public bool IsConnected() => _client.IsConnected;

        static MqttQualityOfServiceLevel MapQos(Qos qos) => (MqttQualityOfServiceLevel)(int)qos;

        void HandleConnection(MqttClientConnectedEventArgs _) => _connectionStatusListener?.onConnected(this);

        void HandleDisconnection(MqttClientDisconnectedEventArgs disconnectedEvent) => _connectionStatusListener?.onDisconnected(this, disconnectedEvent.Exception);

        void ValidateConnectResult(MqttClientAuthenticateResult authenticateResult)
        {
            if(authenticateResult.ResultCode != MqttClientConnectResultCode.Success)
            {
                throw new MqttException(isTemporary: IsTemporaryFailure(authenticateResult.ResultCode), message: $"Authentication failed: [code={authenticateResult.ResultCode}, message={authenticateResult.ReasonString}].");
            }
        }

        void ValidatePublishResult(MqttClientPublishResult publishResult)
        {
            if (publishResult.ReasonCode != MqttClientPublishReasonCode.Success)
            {
                throw new MqttException(isTemporary: IsTemporaryFailure(publishResult.ReasonCode), message: $"Publish failed: [code={publishResult.ReasonCode}, message={publishResult.ReasonString}].");
            }
        }

        bool IsTemporaryFailure(MqttClientConnectResultCode connectResultCode) => connectResultCode == MqttClientConnectResultCode.ServerUnavailable
                || connectResultCode == MqttClientConnectResultCode.ServerBusy
                || connectResultCode == MqttClientConnectResultCode.QuotaExceeded
                || connectResultCode == MqttClientConnectResultCode.ConnectionRateExceeded;

        bool IsTemporaryFailure(MqttClientPublishReasonCode publishResultCode) => publishResultCode == MqttClientPublishReasonCode.QuotaExceeded;

    }
}
