// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Channels;
    using System.Threading.Tasks;
    using M2Mqtt;
    using M2Mqtt.Messages;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class MqttBridgeConnector : IMqttBridgeConnector
    {
        const string BridgeClientId = "EH-BRIDGE";
        const int ReconnectDelayMs = 2000;

        readonly IComponentDiscovery components;

        // List of message ACK waiting on. Although the code that uses the collection is guarded by a lock, still using
        // Concurrent dictionarty to have 'TryAdd'
        readonly ConcurrentDictionary<ushort, TaskCompletionSource<bool>> pendingAcks = new ConcurrentDictionary<ushort, TaskCompletionSource<bool>>();

        readonly object guard = new object();

        Option<Channel<MqttPublishInfo>> publications;
        Option<Task> forwardingLoop;
        Option<MqttClient> mqttClient;

        public MqttBridgeConnector(IComponentDiscovery components)
        {
            this.components = components;

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
                else
                {
                    this.mqttClient = Option.Some(new MqttClient(serverAddress, port, false, MqttSslProtocols.None, null, null));
                }

                client = this.mqttClient.Expect(() => new Exception("No mqtt-bridge connector instance found to setup"));
            }

            // if ConnectAsync is supposed to manage starting it with broker down,
            // put a look here to keep trying - see 'TriggerReconnect' below
            var isConnected = await TryConnectAsync(client, this.components.Subscribers);

            if (!isConnected)
            {
                lock (this.guard)
                {
                    this.mqttClient = Option.None<MqttClient>();
                }

                throw new Exception("Failed to start mqtt-bridge connector");
            }

            client.MqttMsgPublished += this.ConfirmPublished;
            client.MqttMsgPublishReceived += this.ForwardPublish;
            client.ConnectionClosed += this.TriggerReconnect;

            this.publications = Option.Some(Channel.CreateUnbounded<MqttPublishInfo>(
                                    new UnboundedChannelOptions
                                    {
                                        SingleReader = true,
                                        SingleWriter = true
                                    }));

            this.forwardingLoop = Option.Some(this.StartForwardingLoop());

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

                            c.Disconnect();

                            this.publications.ForEach(channel => channel.Writer.Complete());

                            await this.forwardingLoop.ForEachAsync(loop => loop);

                            this.forwardingLoop = Option.None<Task>();
                            this.publications = Option.None<Channel<MqttPublishInfo>>();

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

            // need the lock, otherwise it can happen the the ACK comes back sooner as the id is
            // put into the dictionary next line, causeing the ACK being unknown
            lock (this.guard)
            {
                var messageId = client.Publish(topic, payload, 1, false);
                added = this.pendingAcks.TryAdd(messageId, tcs);
            }

            if (!added)
            {
                // if this happens it means that previously a message was sent out with the same message id but
                // then it wasn't deleted from the penging acks. that is either we went around with all the message ids
                // or some program error didn't delete it. now much to do either way.
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
                var client = default(MqttClient);

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

                    isConnected = await TryConnectAsync(client, this.components.Subscribers);
                }

                client.ConnectionClosed += this.TriggerReconnect;
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

                                        foreach (var consumer in this.components.Consumers)
                                        {
                                            try
                                            {
                                                await consumer.HandleAsync(publishInfo);
                                                Events.MessageForwarded(publishInfo.Topic, publishInfo.Payload.Length);
                                            }
                                            catch (Exception e)
                                            {
                                                Events.FailedToForward(e);

                                                // keep going...
                                            }
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

        // these are statics, so they don't use the state to acquire 'client' - making easier to handle parallel
        // Reconnect/Disconnect cases
        static async Task<bool> TryConnectAsync(MqttClient client, IReadOnlyCollection<IMqttSubscriber> subscribers)
        {
            try
            {
                var result = client.Connect(BridgeClientId);

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

        static async Task SubscribeAsync(MqttClient client, IReadOnlyCollection<IMqttSubscriber> subscribers)
        {
            var expectedAckCount = subscribers.Count;

            if (expectedAckCount > 0)
            {
                using (var acksArrived = new SemaphoreSlim(0, 1))
                {
                    var allQosGranted = default(bool);

                    client.MqttMsgSubscribed += ConfirmSubscribe;

                    foreach (var subscriber in subscribers)
                    {
                        client.Subscribe(
                            subscriber.Subscriptions.ToArray(),
                            Enumerable.Range(1, subscriber.Subscriptions.Count).Select(i => (byte)1).ToArray());
                    }

                    await acksArrived.WaitAsync();

                    client.MqttMsgSubscribed -= ConfirmSubscribe;

                    if (!Volatile.Read(ref allQosGranted))
                    {
                        Events.QosMismatch();
                        throw new Exception("MQTT server did not grant QoS-1 for every requested subscription");
                    }

                    void ConfirmSubscribe(object sender, MqttMsgSubscribedEventArgs e)
                    {
                        if (Interlocked.Decrement(ref expectedAckCount) == 0)
                        {
                            Volatile.Write(ref allQosGranted, e.GrantedQoSLevels.All(qos => qos == 1));

                            acksArrived.Release();
                        }
                    }
                }
            }
        }

        static class Events
        {
            const int IdStart = MqttBridgeEventIds.MqttBridgeConnector;
            static readonly ILogger Log = Logger.Factory.CreateLogger<MqttBridgeConnector>();

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
                FailedToForward
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
            public static void CouldNotForwardMessage(string topic, int len) => Log.LogWarning((int)EventIds.CouldNotForwardMessage, "Could not forward MQTT message from connector. Topic [{0}], Msg. len [{1}] bytes", topic, len);
            public static void ForwardingLoopStarted() => Log.LogInformation((int)EventIds.ForwardingLoopStarted, "Forwarding loop started");
            public static void ForwardingLoopStopped() => Log.LogInformation((int)EventIds.ForwardingLoopStopped, "Forwarding loop stopped");
            public static void MessageForwarded(string topic, int len) => Log.LogDebug((int)EventIds.MessageForwarded, "Message forwarded. Topic [{0}], Msg. len [{1}] bytes", topic, len);
            public static void FailedToForward(Exception e) => Log.LogError((int)EventIds.FailedToForward, e, "Failed to forward message.");
        }
    }
}
