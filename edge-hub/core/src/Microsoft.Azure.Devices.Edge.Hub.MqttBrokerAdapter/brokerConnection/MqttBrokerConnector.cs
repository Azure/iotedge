// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Channels;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter.Exceptions;
    using Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter.Interfaces;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class MqttBrokerConnector : IMqttBrokerConnector, IConnectionStatusListener, IMessageHandler
    {
        const int ReconnectDelayMs = 2000;
        static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(10);
        readonly IComponentDiscovery components;
        readonly ISystemComponentIdProvider systemComponentIdProvider;
        readonly Dictionary<ushort, TaskCompletionSource<bool>> pendingAcks = new Dictionary<ushort, TaskCompletionSource<bool>>();
        readonly IMqttClientProvider _mqttClientProvider;
        readonly object guard = new object();
        bool _connecting;

        Option<Channel<MqttPublishInfo>> publications;
        Option<Task> forwardingLoop;
        Option<IMqttClient> mqttClient;

        public MqttBrokerConnector(IMqttClientProvider mqttClientProvider, IComponentDiscovery components, ISystemComponentIdProvider systemComponentIdProvider)
        {
            this._mqttClientProvider = Preconditions.CheckNotNull(mqttClientProvider, nameof(mqttClientProvider));
            this.components = Preconditions.CheckNotNull(components, nameof(components));
            this.systemComponentIdProvider = Preconditions.CheckNotNull(systemComponentIdProvider, nameof(systemComponentIdProvider));

            // because of the circular dependency between MqttBridgeConnector and the producers,
            // in this loop the producers get the IMqttBridgeConnector reference:
            foreach (var producer in components.Producers)
            {
                producer.SetConnector(this);
            }
        }

        public async Task ConnectAsync(string serverAddress, int port)
        {
            Events.Starting();

            IMqttClient client;

            lock (this.guard)
            {
                if (this.mqttClient.HasValue)
                {
                    Events.StartedWhenAlreadyRunning();
                    throw new InvalidOperationException("Cannot start mqtt-bridge connector twice");
                }

                client = _mqttClientProvider.CreateMqttClient(serverAddress, port, false);
                client.RegisterConnectionStatusListener(this);
                client.RegisterMessageHandler(this);
                this.mqttClient =  Option.Some(client);
                _connecting = true;
            }

            this.publications = Option.Some(Channel.CreateUnbounded<MqttPublishInfo>(
                                    new UnboundedChannelOptions
                                    {
                                        SingleReader = true,
                                        SingleWriter = true
                                    }));

            this.forwardingLoop = Option.Some(this.StartForwardingLoop());

            // if ConnectAsync is supposed to manage starting it with broker down,
            // put a loop here to keep trying - see 'TriggerReconnect' below
            var isConnected = await TryConnectAsync(client, this.components.Consumers, this.systemComponentIdProvider.EdgeHubBridgeId);

            if (!isConnected)
            {
                await this.StopForwardingLoopAsync();

                lock (this.guard)
                {
                    this.mqttClient = Option.None<IMqttClient>();
                }

                Events.CouldNotConnect();
                throw new Exception("Failed to start MQTT broker connector");
            }

            Events.Started();
        }

        public async Task DisconnectAsync()
        {
            Events.Closing();

            Option<IMqttClient> clientToStop;

            lock (this.guard)
            {
                clientToStop = this.mqttClient;
                this.mqttClient = Option.None<IMqttClient>();
                _connecting = false;
            }

            try
            {
                await clientToStop.ForEachAsync(
                        async client =>
                        {
                            try
                            {
                                await client.DisconnectAsync(new CancellationTokenSource(OperationTimeout).Token);
                            }
                            catch
                            {
                                // swallowing: when the container is shutting down, it is possible that the broker disconnected.
                                // in those case an internal socket will be deleted and this Disconnect() call ends up in a
                                // Disposed() exception.
                            }

                            await this.StopForwardingLoopAsync();

                            Events.Closed();
                        },
                        () =>
                        {
                            Events.ClosedWhenNotRunning();
                            throw new InvalidOperationException("Cannot stop mqtt-bridge connector when not running");
                        });
            }
            finally
            {
                foreach (var tcs in this.pendingAcks.Values)
                {
                    tcs.SetCanceled();
                }

                this.pendingAcks.Clear();
            }
        }

        public async Task<bool> SendAsync(string topic, byte[] payload)
        {
            var client = this.mqttClient.Expect(() => new Exception("No mqtt-bridge connector instance found to send messages."));

            // need the lock, otherwise it can happen the the ACK comes back sooner as the id is
            // put into the dictionary next line, causeing the ACK being unknown.
            await client.PublishAsync(topic, payload, Qos.AtLeastOnce, new CancellationTokenSource(OperationTimeout).Token);
            return true;
        }

        void TriggerReconnect(IMqttClient mqttClient)
        {
            lock (this.guard)
            {
                if (_connecting || !this.mqttClient.Contains(mqttClient))
                {
                    // supress reconnect while re-connecting or disconnect called
                    return;
                }
                _connecting = true;
            }

            Events.Disconnected();
            Task.Run(async () =>
            {
                IMqttClient client;
                var isConnected = false;

                while (!isConnected)
                {
                    await Task.Delay(ReconnectDelayMs);

                    lock (this.guard)
                    {
                        // seems Disconnect has been called since.
                        if (!this.mqttClient.Contains(mqttClient))
                        {
                            return;
                        }

                        client = this.mqttClient.Expect(() => new Exception("No mqtt-bridge connector instance found to use"));
                    }

                    isConnected = await TryConnectAsync(client, this.components.Consumers, this.systemComponentIdProvider.EdgeHubBridgeId);

                    lock (this.guard)
                    {
                        if (isConnected && client.IsConnected())
                        {
                            // re-connected
                            _connecting = false;
                        }
                        else
                        {
                            // already disconnected
                            isConnected = false;
                        }
                    }
                }

            });
        }

        Task StartForwardingLoop()
        {
            var loopTask = Task.Run(
                                async () =>
                                {
                                    Events.ForwardingLoopStarted();
                                    while (await this.publications.Expect(ChannelIsBroken).Reader.WaitToReadAsync())
                                    {
                                        var publishInfo = default(MqttPublishInfo);

                                        try
                                        {
                                            publishInfo = await this.publications.Expect(ChannelIsBroken).Reader.ReadAsync();
                                        }
                                        catch (Exception e)
                                        {
                                            Events.FailedToForward(e);
                                            continue;
                                        }

                                        var forwarded = false;
                                        foreach (var consumer in this.components.Consumers)
                                        {
                                            try
                                            {
                                                var accepted = await consumer.HandleAsync(publishInfo);
                                                forwarded |= accepted;
                                                Events.MessageForwarded(consumer.GetType().Name, accepted, publishInfo.Topic, publishInfo.Payload.Length);
                                            }
                                            catch (Exception e)
                                            {
                                                Events.FailedToForward(e);
                                                // Keep going with other consumers...
                                            }
                                        }

                                        if (!forwarded)
                                        {
                                            Events.FailedToForward(new Exception("Message dropped: no consumer accepted."));
                                        }
                                    }

                                    foreach (var consumer in this.components.Consumers)
                                    {
                                        consumer.ProducerStopped();
                                    }

                                    Events.ForwardingLoopStopped();
                                });

            return loopTask;

            static Exception ChannelIsBroken()
            {
                return new Exception("Channel is broken, exiting forwarding loop by error");
            }
        }

        async Task StopForwardingLoopAsync()
        {
            this.publications.ForEach(channel => channel.Writer.Complete());

            await this.forwardingLoop.ForEachAsync(loop => loop);

            this.forwardingLoop = Option.None<Task>();
            this.publications = Option.None<Channel<MqttPublishInfo>>();
        }

        // these are statics, so they don't use the state to acquire 'client' - making easier to handle parallel
        // Reconnect/Disconnect cases
        static async Task<bool> TryConnectAsync(IMqttClient client, IReadOnlyCollection<IMessageConsumer> subscribers, string id)
        {
            Events.AttemptConnect();
            try
            {
                await client.ConnectAsync(id, id, string.Empty, new CancellationTokenSource(OperationTimeout).Token);
                await SubscribeAsync(client, subscribers);
                return false;
            }
            catch (Exception e)
            {
                Events.FailedToConnect(e);
                // if the connection was successful at some level,
                // try to clean up with Disconnect()
                if (client.IsConnected())
                {
                    try
                    {
                        await client.DisconnectAsync(new CancellationTokenSource(OperationTimeout).Token);
                    }
                    catch
                    {
                        // swallow intentionally
                    }

                }

                return false;
            }
        }

        static async Task SubscribeAsync(IMqttClient client, IReadOnlyCollection<IMessageConsumer> subscribers)
        {
            var subscriptions = new Dictionary<string, Qos>();
            foreach (var subscriber in subscribers)
            {
                foreach(var topic in subscriber.Subscriptions)
                {
                    subscriptions[topic] = Qos.AtLeastOnce;
                }
            }

            var cancellationToken = new CancellationTokenSource(OperationTimeout).Token;
            var gainedSubscriptions = await client.SubscribeAsync(subscriptions, cancellationToken);
            ValidateSubscribeResult(subscriptions, gainedSubscriptions);
        }

        static void ValidateSubscribeResult(Dictionary<string, Qos> expectedSubscriptions, Dictionary<string, Qos> gainedSubscriptions)
        {
            foreach (var expectedSubscription in expectedSubscriptions)
            {
                gainedSubscriptions.TryGetValue(expectedSubscription.Key, out var qos);
                if (expectedSubscription.Value != qos)
                {
                    Events.QosMismatch();
                    throw new MqttException(message: $"Subscribe [topiic={expectedSubscription.Key}, qos={expectedSubscription.Value} failed: gained={qos}].");
                }
            }
        }

        public void onConnected(IMqttClient mqttClient) => Events.Connected();

        public void onDisconnected(IMqttClient mqttClient, Exception exception) => TriggerReconnect(mqttClient);

        public Task<bool> ProcessMessageAsync(string topic, byte[] payload)
        {
            var isWritten = this.publications.Match(
                                    channel => channel.Writer.TryWrite(new MqttPublishInfo(topic, payload)),
                                    () => false);

            if (!isWritten)
            {
                // Dropping the message
                Events.CouldNotForwardMessage(topic, payload.Length);
            }

            return Task.FromResult(true);
        }

        static class Events
        {
            const int IdStart = MqttBridgeEventIds.MqttBridgeConnector;
            static readonly ILogger Log = Logger.Factory.CreateLogger<MqttBrokerConnector>();

            enum EventIds
            {
                Starting = IdStart,
                Started,
                Closing,
                Closed,
                ClosedWhenNotRunning,
                StartedWhenAlreadyRunning,
                MqttServerDenied,
                FailedToConnect,
                QosMismatch,
                UnknownMessageId,
                CouldNotForwardMessage,
                ForwardingLoopStarted,
                ForwardingLoopStopped,
                MessageForwarded,
                FailedToForward,
                CouldNotConnect,
                Connected,
                AttemptConnect,
                Disconnected
            }

            public static void Starting() => Log.LogInformation((int)EventIds.Starting, "Starting mqtt-bridge connector");
            public static void Started() => Log.LogInformation((int)EventIds.Started, "Started mqtt-bridge connector");
            public static void Closing() => Log.LogInformation((int)EventIds.Closing, "Closing mqtt-bridge connector");
            public static void Closed() => Log.LogInformation((int)EventIds.Closed, "Closed mqtt-bridge connector");
            public static void ClosedWhenNotRunning() => Log.LogInformation((int)EventIds.ClosedWhenNotRunning, "Closed mqtt-bridge connector when it was not running");
            public static void StartedWhenAlreadyRunning() => Log.LogWarning((int)EventIds.StartedWhenAlreadyRunning, "Started mqtt-bridge connector when it was already running");
            public static void MqttServerDenied() => Log.LogError((int)EventIds.MqttServerDenied, "MQTT Server did not allow mqtt-bridge to connect");
            public static void FailedToConnect(Exception e) => Log.LogError((int)EventIds.FailedToConnect, e, "Mqtt-bridge connector failed to connect");
            public static void QosMismatch() => Log.LogError((int)EventIds.QosMismatch, "MQTT server did not grant QoS for every requested subscription");
            public static void UnknownMessageId(ushort id) => Log.LogError((int)EventIds.UnknownMessageId, "Unknown message id received : {0}", id);
            public static void CouldNotForwardMessage(string topic, int len) => Log.LogWarning((int)EventIds.CouldNotForwardMessage, "Could not forward MQTT message from connector. Topic {0}, Msg. len {1} bytes", topic, len);
            public static void ForwardingLoopStarted() => Log.LogInformation((int)EventIds.ForwardingLoopStarted, "Forwarding loop started");
            public static void ForwardingLoopStopped() => Log.LogInformation((int)EventIds.ForwardingLoopStopped, "Forwarding loop stopped");
            public static void MessageForwarded(string consumer, bool accepted, string topic, int len) => Log.LogDebug((int)EventIds.MessageForwarded, "Message forwarded to {0} and it {1}. Topic {2}, Msg. len {3} bytes", consumer, accepted ? "accepted" : "ignored", topic, len);
            public static void FailedToForward(Exception e) => Log.LogError((int)EventIds.FailedToForward, e, "Failed to forward message.");
            public static void CouldNotConnect() => Log.LogInformation((int)EventIds.CouldNotConnect, "Could not connect to MQTT Broker, possibly it is not running. To disable MQTT Broker Connector, please set 'mqttBrokerSettings__enabled' environment variable to 'false'");
            public static void Connected() => Log.LogInformation((int)EventIds.Connected, "Connected to Mqtt-bridge");
            public static void AttemptConnect() => Log.LogInformation((int)EventIds.AttemptConnect, "Attempt to connect to Mqtt-bridge...");
            public static void Disconnected() => Log.LogInformation((int)EventIds.Disconnected, "Disconnected to Mqtt-bridge.");
        }
    }
}
