// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Channels;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Extensions.Logging;
    using uPLibrary.Networking.M2Mqtt;
    using uPLibrary.Networking.M2Mqtt.Messages;

    public class MqttBrokerConnector : IMqttBrokerConnector
    {
        const int ReconnectDelayMs = 2000;
        const int SubAckTimeoutSecs = 10;

        readonly IComponentDiscovery components;
        readonly ISystemComponentIdProvider systemComponentIdProvider;
        readonly Dictionary<ushort, TaskCompletionSource<bool>> pendingAcks = new Dictionary<ushort, TaskCompletionSource<bool>>();

        readonly object guard = new object();

        Option<Channel<MqttPublishInfo>> publications;
        Option<Task> forwardingLoop;
        Option<MqttClient> mqttClient;

        AtomicBoolean isRetrying = new AtomicBoolean(false);

        public event EventHandler OnConnected;

        public MqttBrokerConnector(IComponentDiscovery components, ISystemComponentIdProvider systemComponentIdProvider)
        {
            this.components = Preconditions.CheckNotNull(components);
            this.systemComponentIdProvider = Preconditions.CheckNotNull(systemComponentIdProvider);

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

            var client = default(MqttClient);

            lock (this.guard)
            {
                if (this.mqttClient.HasValue)
                {
                    Events.StartedWhenAlreadyRunning();
                    throw new InvalidOperationException("Cannot start mqtt-bridge connector twice");
                }

                client = new MqttClient(serverAddress, port, false, MqttSslProtocols.None, null, null);
                this.mqttClient = Option.Some(client);
            }

            client.MqttMsgPublished += this.ConfirmPublished;
            client.MqttMsgPublishReceived += this.ForwardPublish;

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
                client.MqttMsgPublished -= this.ConfirmPublished;
                client.MqttMsgPublishReceived -= this.ForwardPublish;

                await this.StopForwardingLoopAsync();

                lock (this.guard)
                {
                    this.mqttClient = Option.None<MqttClient>();
                }

                Events.CouldNotConnect();
                throw new Exception("Failed to start MQTT broker connector");
            }

            client.ConnectionClosed += this.TriggerReconnect;

            if (!client.IsConnected)
            {
                // at this point it is not known that 'TriggerReconnect' was subscribed in time,
                // let's trigger it manually - if started twice, that is not a problem
                this.TriggerReconnect(this, new EventArgs());
            }

            this.OnConnected?.Invoke(this, EventArgs.Empty);
            Events.Started();
        }

        public async Task DisconnectAsync()
        {
            Events.Closing();

            Option<MqttClient> clientToStop;

            lock (this.guard)
            {
                clientToStop = this.mqttClient;
                this.mqttClient = Option.None<MqttClient>();
            }

            try
            {
                await clientToStop.ForEachAsync(
                        async c =>
                        {
                            c.MqttMsgPublished -= this.ConfirmPublished;
                            c.MqttMsgPublishReceived -= this.ForwardPublish;
                            c.ConnectionClosed -= this.TriggerReconnect;

                            if (c.IsConnected)
                            {
                                try
                                {
                                    c.Disconnect();
                                }
                                catch
                                {
                                    // swallowing: when the container is shutting down, it is possible that the broker disconnected.
                                    // in those case an internal socket will be deleted and this Disconnect() call ends up in a
                                    // Disposed() exception.
                                }
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

            var added = default(bool);
            var tcs = new TaskCompletionSource<bool>();

            // need the lock, otherwise it can happen the ACK comes back sooner as the id is
            // put into the dictionary next line, causing the ACK being unknown.
            lock (this.guard)
            {
                var messageId = client.Publish(topic, payload, 1, false);
                added = this.pendingAcks.TryAdd(messageId, tcs);
            }

            if (!added)
            {
                // if this happens it means that previously a message was sent out with the same message id but
                // then it wasn't deleted from the penging acks. that is either we went around with all the message ids
                // or some program error didn't delete it. not much to do either way.
                new Exception("Could not store message id to monitor Mqtt ACK");
            }

            var result = await tcs.Task;

            return result;
        }

        void ForwardPublish(object sender, MqttMsgPublishEventArgs e)
        {
            var isWritten = this.publications.Match(
                                    channel => channel.Writer.TryWrite(new MqttPublishInfo(e.Topic, e.Message)),
                                    () => false);

            if (!isWritten)
            {
                // Dropping the message
                Events.CouldNotForwardMessage(e.Topic, e.Message.Length);
            }
        }

        void ConfirmPublished(object sender, MqttMsgPublishedEventArgs e)
        {
            lock (this.guard)
            {
                if (this.pendingAcks.TryRemove(e.MessageId, out TaskCompletionSource<bool> tcs))
                {
                    tcs.SetResult(e.IsPublished);
                }
                else
                {
                    Events.UnknownMessageId(e.MessageId);
                }
            }
        }

        void TriggerReconnect(object sender, EventArgs e)
        {
            Task.Run(async () =>
            {
                if (this.isRetrying.GetAndSet(true))
                {
                    return;
                }

                var client = default(MqttClient);

                try
                {
                    lock (this.guard)
                    {
                        if (!this.mqttClient.HasValue)
                        {
                            return;
                        }

                        client = this.mqttClient.Expect(() => new Exception("No mqtt-bridge connector instance found to use"));
                    }

                    // don't trigger it to ourselves while we are trying
                    client.ConnectionClosed -= this.TriggerReconnect;

                    var isConnected = false;

                    while (!isConnected)
                    {
                        await Task.Delay(ReconnectDelayMs);

                        lock (this.guard)
                        {
                            // seems Disconnect has been called since.
                            if (!this.mqttClient.HasValue)
                            {
                                return;
                            }

                            client = this.mqttClient.Expect(() => new Exception("No mqtt-bridge connector instance found to use"));
                        }

                        isConnected = await TryConnectAsync(client, this.components.Consumers, this.systemComponentIdProvider.EdgeHubBridgeId);
                    }

                    client.ConnectionClosed += this.TriggerReconnect;
                }
                finally
                {
                    this.isRetrying.Set(false);
                }

                if (!client.IsConnected)
                {
                    // at this point it is not known that 'TriggerReconnect' was subscribed in time,
                    // let's trigger it manually - if started twice, that is not a problem
                    this.TriggerReconnect(this, new EventArgs());
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

                                        var accepted = false;
                                        foreach (var consumer in this.components.Consumers)
                                        {
                                            try
                                            {
                                                accepted = await consumer.HandleAsync(publishInfo);
                                                if (accepted)
                                                {
                                                    Events.MessageForwarded(consumer.GetType().Name, accepted, publishInfo.Topic, publishInfo.Payload.Length);
                                                    break;
                                                }
                                            }
                                            catch (Exception e)
                                            {
                                                Events.FailedToForward(e);
                                                // Keep going with other consumers...
                                            }
                                        }

                                        if (!accepted)
                                        {
                                            Events.MessageNotForwarded(publishInfo.Topic, publishInfo.Payload.Length);
                                        }
                                    }

                                    Events.ForwardingLoopStopped();
                                });

            return loopTask;

            Exception ChannelIsBroken()
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
        static async Task<bool> TryConnectAsync(MqttClient client, IReadOnlyCollection<IMessageConsumer> subscribers, string id)
        {
            try
            {
                var result = client.Connect(id, id, string.Empty);

                if (result != MqttMsgConnack.CONN_ACCEPTED)
                {
                    Events.MqttServerDenied();
                    throw new Exception("Mqtt server rejected bridge connection");
                }

                await SubscribeAsync(client, subscribers);

                return true;
            }
            catch (Exception e)
            {
                Events.FailedToConnect(e);

                // if the connection was successful at some level,
                // try to clean up with Disconnect()
                if (client.IsConnected)
                {
                    try
                    {
                        client.Disconnect();
                    }
                    catch
                    {
                        // swallow intentionally
                    }
                }

                return false;
            }
        }

        static async Task SubscribeAsync(MqttClient client, IReadOnlyCollection<IMessageConsumer> subscribers)
        {
            var subscribersWithSubscriptions = subscribers.Where(s => s.Subscriptions != null && s.Subscriptions.Count > 0).ToArray();
            var expectedAckCount = new AtomicLong(subscribersWithSubscriptions.Count());

            if (expectedAckCount.Get() > 0)
            {
                using (var acksArrived = new SemaphoreSlim(0, 1))
                {
                    var allQosGranted = new AtomicBoolean(false);

                    client.MqttMsgSubscribed += ConfirmSubscribe;

                    foreach (var subscriber in subscribersWithSubscriptions)
                    {
                        client.Subscribe(
                            subscriber.Subscriptions.ToArray(),
                            Enumerable.Range(1, subscriber.Subscriptions.Count).Select(i => (byte)1).ToArray());
                    }

                    try
                    {
                        await acksArrived.WaitAsync(TimeSpan.FromSeconds(SubAckTimeoutSecs));
                    }
                    catch (Exception ex)
                    {
                        Events.TimeoutReceivingSubAcks(ex);
                        throw new TimeoutException("MQTT server did not acknowledge subscriptions", ex);
                    }
                    finally
                    {
                        client.MqttMsgSubscribed -= ConfirmSubscribe;
                    }

                    if (!allQosGranted.Get())
                    {
                        Events.QosMismatch();
                        throw new Exception("MQTT server did not grant QoS-1 for every requested subscription");
                    }

                    void ConfirmSubscribe(object sender, MqttMsgSubscribedEventArgs e)
                    {
                        if (expectedAckCount.Decrement() == 0)
                        {
                            allQosGranted.Set(e.GrantedQoSLevels.All(qos => qos == 1));

                            acksArrived.Release();
                        }
                    }
                }
            }
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
                MessageNotForwarded,
                FailedToForward,
                CouldNotConnect,
                TimeoutReceivingSubAcks
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
            public static void MessageNotForwarded(string topic, int len) => Log.LogDebug((int)EventIds.MessageForwarded, "Message has not been forwarded to any consumers. Topic {0}, Msg. len {1} bytes", topic, len);
            public static void FailedToForward(Exception e) => Log.LogError((int)EventIds.FailedToForward, e, "Failed to forward message.");
            public static void CouldNotConnect() => Log.LogInformation((int)EventIds.CouldNotConnect, "Could not connect to MQTT Broker, possibly it is not running. To disable MQTT Broker Connector, please set 'mqttBrokerSettings__enabled' environment variable to 'false'");
            public static void TimeoutReceivingSubAcks(Exception e) => Log.LogError((int)EventIds.TimeoutReceivingSubAcks, e, "MQTT Broker has not acknowledged subscriptions in time");
        }
    }
}
