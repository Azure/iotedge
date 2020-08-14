// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter.Exceptions;
using Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter.Interfaces;
using Microsoft.Azure.Devices.Edge.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;

namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter.M2Mqtt
{
    class M2MqttClient : IMqttClient
    {
        private readonly MqttClient _client;
        private readonly object _lock;
        private readonly Dictionary<ushort, TaskCompletionSource<bool>> _pendingVoidTasks;
        private readonly Dictionary<ushort, TaskCompletionSource<byte[]>> _pendingSubscribes;
        private IConnectionStatusListener _connectionStatusListener;
        private IMessageHandler _messageHandler;

        public M2MqttClient(string host, int port, bool isSsl)
        {
            Preconditions.CheckNotNull(host, nameof(host));
            _lock = new object();
            _pendingVoidTasks = new Dictionary<ushort, TaskCompletionSource<bool>>();
            _pendingSubscribes = new Dictionary<ushort, TaskCompletionSource<byte[]>>();
            _client = new MqttClient(host, port, isSsl, MqttSslProtocols.None, null, null);
            _client.MqttMsgSubscribed += OnTopicSubscribed;
            _client.MqttMsgUnsubscribed += OnTopicUnsubscribed;
            _client.MqttMsgPublished += OnMessagePublished;
            _client.MqttMsgPublishReceived += OnMessageReceived;
            _client.ConnectionClosed += HandleDisconnection;
        }

        public Task ConnectAsync(string clientId, string username, string password, CancellationToken cancellationToken) => ConnectAsync(clientId, username, password, true, default, cancellationToken);

        public Task ConnectAsync(string clientId, string username, string password, bool cleanSession, TimeSpan keepAlivePeriod, CancellationToken cancellationToken)
        {
            Task<bool> connectTaskSupplier()
            {
                var result = _client.Connect(clientId, username, password, cleanSession, Convert.ToUInt16(keepAlivePeriod.TotalSeconds));
                if (result != MqttMsgConnack.CONN_ACCEPTED)
                {
                    throw new MqttException(isTemporary: result == MqttMsgConnack.CONN_REFUSED_SERVER_UNAVAILABLE, message: $"Mqtt server rejected bridge connection with ConAck={result}");
                }
                
                _connectionStatusListener?.onConnected(this);
                return Task.FromResult(true);
            }

            Task restoreTaskSupplier()
            {
                try
                {
                    _client.Disconnect();
                }
                catch
                {
                    // ignore exception since it's already failed
                }
                return Task.FromResult(true);
            }

            return RunTaskWithCancellationTokenAsync(connectTaskSupplier, restoreTaskSupplier, cancellationToken);
        }

        public Task DisconnectAsync(CancellationToken cancellationToken)
        {
            _client.Disconnect();
            lock (_lock)
            {
                foreach(var source in _pendingVoidTasks.Values)
                {
                    source.TrySetCanceled();
                }
                _pendingVoidTasks.Clear();

                foreach (var source in _pendingSubscribes.Values)
                {
                    source.TrySetCanceled();
                }
                _pendingSubscribes.Clear();
            }
            return Task.FromResult(true);
        }

        public async Task PublishAsync(string topic, byte[] payload, Qos qos, CancellationToken cancellationToken)
        {
            Preconditions.CheckNonWhiteSpace(topic, nameof(topic));
            if (qos == Qos.ExactlyOnce)
            {
                throw new MqttException(message: "QOS ExactlyOnce is not supported.");
            }

            if (qos == Qos.AtMostOnce)
            {
                _client.Publish(topic, payload, Convert.ToByte((int) qos), false);
                return;
            }

            var source = new TaskCompletionSource<bool>();
            Task<bool> sendTaskSupplier()
            {
                lock (_lock)
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        var messageId = _client.Publish(topic, payload, 1, false);
                        _pendingVoidTasks.TryAdd(messageId, source);
                    }
                }

                return source.Task;
            }

            Task restoreTaskSupplier()
            {
                source.TrySetCanceled();
                return Task.FromResult(true);
            }

            var published = await RunTaskWithCancellationTokenAsync(sendTaskSupplier, restoreTaskSupplier, cancellationToken);
            if (!published)
            {
                throw new MqttException(message: "Publish message failed.");
            }
        }

        public Task<Dictionary<string, Qos>> SubscribeAsync(Dictionary<string, Qos> subscriptions, CancellationToken cancellationToken)
        {
            Preconditions.CheckNotNull(subscriptions, nameof(subscriptions));
            Preconditions.CheckArgument(subscriptions.Count > 0, "No subscriptions.");
            var source = new TaskCompletionSource<byte[]>();
            var topics = new List<string>();
            var qoses = new List<byte>();
            foreach (var entry in subscriptions)
            {
                topics.Add(entry.Key);
                qoses.Add(Convert.ToByte((int)entry.Value));
            }

            async Task<Dictionary<string, Qos>> subscribeTaskSupplier()
            {
                lock (_lock)
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        var subscribeId = _client.Subscribe(topics.ToArray(), qoses.ToArray());
                        _pendingSubscribes.TryAdd(subscribeId, source);
                    }
                }

                var subscribeResult = await source.Task;
                if (subscribeResult.Length != topics.Count)
                {
                    throw new MqttException(message: $"MQTT server did not grant QoS for every requested subscription, expected={subscriptions.Count}, but was {subscribeResult.Length}.");
                }

                var gainedQoses = new Dictionary<string, Qos>();
                for (var i=0; i<subscribeResult.Length; i++)
                {
                    var gained = Convert.ToInt32(subscribeResult[i]);
                    if (gained < 0 || gained > 2)
                    {
                        throw new MqttException(message: $"Subscribe [topiic={topics[i]}, qos={qoses[i]} failed: [resultCode={gained}].");
                    }
                    gainedQoses[topics[i]] = (Qos)gained;
                }

                return gainedQoses;
            }

            Task restoreTaskSupplier()
            {
                source.TrySetCanceled();
                return Task.FromResult(true);
            }

            return RunTaskWithCancellationTokenAsync(subscribeTaskSupplier, restoreTaskSupplier, cancellationToken);
        }

        public Task UnsubscribeAsync(IEnumerable<string> topics, CancellationToken cancellationToken)
        {
            Preconditions.CheckNotNull(topics, nameof(topics));
            Preconditions.CheckArgument(topics.Count() > 0, "No topics.");

            var source = new TaskCompletionSource<bool>();
            Task<bool> unsubscribeTaskSupplier()
            {
                lock (_lock)
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        var subscribeId = _client.Unsubscribe(topics.ToArray());
                        _pendingVoidTasks.TryAdd(subscribeId, source);
                    }
                }

                return source.Task;
            }

            Task restoreTaskSupplier()
            {
                source.TrySetCanceled();
                return Task.FromResult(true);
            }

            return RunTaskWithCancellationTokenAsync(unsubscribeTaskSupplier, restoreTaskSupplier, cancellationToken);
        }

        public void RegisterConnectionStatusListener(IConnectionStatusListener connectionStatusListener) => _connectionStatusListener = connectionStatusListener;

        public void RegisterMessageHandler(IMessageHandler messageHandler) => _messageHandler = messageHandler;

        public bool IsConnected() => _client.IsConnected;

        void HandleDisconnection(object sender, EventArgs e) => _connectionStatusListener?.onDisconnected(this, new MqttException(message: e.ToString()));

        void OnMessagePublished(object sender, MqttMsgPublishedEventArgs eventArgs)
        {
            lock (_lock)
            {
                _pendingVoidTasks.Remove(eventArgs.MessageId, out var source);
                source?.TrySetResult(eventArgs.IsPublished);
            }
        }

        void OnMessageReceived(object sender, MqttMsgPublishEventArgs eventArgs) => _messageHandler.ProcessMessageAsync(eventArgs.Topic, eventArgs.Message).GetAwaiter().GetResult();

        void OnTopicSubscribed(object sender, MqttMsgSubscribedEventArgs eventArgs)
        {
            lock (_lock)
            {
                _pendingSubscribes.Remove(eventArgs.MessageId, out var source);
                source?.TrySetResult(eventArgs.GrantedQoSLevels);
            }
        }

        void OnTopicUnsubscribed(object sender, MqttMsgUnsubscribedEventArgs eventArgs)
        {
            lock (_lock)
            {
                _pendingVoidTasks.Remove(eventArgs.MessageId, out var source);
                source?.TrySetResult(true);
            }
        }

        async Task<T> RunTaskWithCancellationTokenAsync<T>(Func<Task<T>> taskSupplier, Func<Task> restoreTaskSupplier, CancellationToken cancellationToken)
        {
            var waitTask = Task.FromCanceled(cancellationToken);
            Task finishedTask;
            Task<T> targetTask;
            try
            {
                targetTask = taskSupplier();
                finishedTask = await Task.WhenAny(targetTask, waitTask);
            }
            catch (Exception e)
            {
                throw new MqttException(cause: e);
            }

            if (finishedTask == waitTask)
            {
                await restoreTaskSupplier();
                throw new MqttException(isTemporary: false, message: "Operation was cancelled.");
            }

            return targetTask.Result;
        }
    }
}
