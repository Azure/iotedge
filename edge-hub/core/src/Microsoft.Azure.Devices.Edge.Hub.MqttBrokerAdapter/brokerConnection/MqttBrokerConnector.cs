// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Channels;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Extensions.Logging;
    using uPLibrary.Networking.M2Mqtt;
    using uPLibrary.Networking.M2Mqtt.Messages;

    public class MqttBrokerConnector : IMqttBrokerConnector
    {
        const int ReconnectDelayMs = 2000;
        const int DefaultAckTimeoutSecs = 10;

        readonly IComponentDiscovery components;
        readonly ISystemComponentIdProvider systemComponentIdProvider;
        readonly Dictionary<ushort, TaskCompletionSource<bool>> pendingAcks = new Dictionary<ushort, TaskCompletionSource<bool>>();

        readonly object guard = new object();

        readonly TaskCompletionSource<bool> onConnectedTcs = new TaskCompletionSource<bool>();

        Option<Channel<MqttPublishInfo>> upstreamPublications;
        Option<Channel<MqttPublishInfo>> downstreamPublications;
        Option<Task> forwardingLoops;
        Option<MqttClient> mqttClient;

        AtomicBoolean isRetrying = new AtomicBoolean(false);

        public TimeSpan AckTimeout { get; set; }

        public Task EnsureConnected => this.onConnectedTcs.Task;

        public MqttBrokerConnector(IComponentDiscovery components, ISystemComponentIdProvider systemComponentIdProvider)
        {
            this.AckTimeout = TimeSpan.FromSeconds(DefaultAckTimeoutSecs);

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

            this.forwardingLoops = Option.Some(this.StartForwardingLoops());

            // if ConnectAsync is supposed to manage starting it with broker down,
            // put a loop here to keep trying - see 'TriggerReconnect' below
            var isConnected = await TryConnectAsync(client, this.components.Consumers, this.systemComponentIdProvider.EdgeHubBridgeId);

            if (!isConnected)
            {
                client.MqttMsgPublished -= this.ConfirmPublished;
                client.MqttMsgPublishReceived -= this.ForwardPublish;

                await this.StopForwardingLoopsAsync();

                lock (this.guard)
                {
                    this.mqttClient = Option.None<MqttClient>();
                }

                Events.CouldNotConnect();
                throw new EdgeHubConnectionException("Failed to start MQTT broker connector");
            }

            client.ConnectionClosed += this.TriggerReconnect;

            if (!client.IsConnected)
            {
                // at this point it is not known that 'TriggerReconnect' was subscribed in time,
                // let's trigger it manually - if started twice, that is not a problem
                this.TriggerReconnect(this, new EventArgs());
            }

            this.onConnectedTcs.SetResult(true);
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

                            await this.StopForwardingLoopsAsync();

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

        public async Task<bool> SendAsync(string topic, byte[] payload, bool retain = false)
        {
            var client = this.mqttClient.Expect(() => new IOException("No mqtt-bridge connector instance found to send messages."));

            var added = default(bool);
            var tcs = new TaskCompletionSource<bool>();

            // need the lock, otherwise it can happen the ACK comes back sooner as the id is
            // put into the dictionary next line, causing the ACK being unknown.
            ushort messageId;
            lock (this.guard)
            {
                messageId = client.Publish(topic, payload, 1, retain);
                added = this.pendingAcks.TryAdd(messageId, tcs);
            }

            if (!added)
            {
                // if this happens it means that previously a message was sent out with the same message id but
                // then it wasn't deleted from the penging acks. that is either we went around with all the message ids
                // or some program error didn't delete it. not much to do either way.
                new IOException("Could not store message id to monitor Mqtt ACK");
            }

            bool result;

            try
            {
                result = await tcs.Task.TimeoutAfter(this.AckTimeout);
            }
            catch
            {
                lock (this.guard)
                {
                    this.pendingAcks.TryRemove(messageId, out TaskCompletionSource<bool> _);
                }

                throw;
            }

            return result;
        }

        void ForwardPublish(object sender, MqttMsgPublishEventArgs e)
        {
            bool isWritten;
            if (!string.IsNullOrEmpty(e.Topic) && e.Topic.StartsWith("$downstream/"))
            {
                // messages from upstream come with prefix downstream - because for the parent we are downstream
                isWritten = this.upstreamPublications.Match(
                                    channel => channel.Writer.TryWrite(new MqttPublishInfo(e.Topic, e.Message)),
                                    () => false);
            }
            else
            {
                isWritten = this.downstreamPublications.Match(
                                    channel => channel.Writer.TryWrite(new MqttPublishInfo(e.Topic, e.Message)),
                                    () => false);
            }

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
            Task.Factory.StartNew(
                async () =>
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
                    catch (Exception)
                    {
                        Events.NoMqttClientWhenReconnecting();
                        return;
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
                },
                TaskCreationOptions.LongRunning);
        }

        Task StartForwardingLoops()
        {
            this.CreateMessageChannels();

            var downstreamTask = Task.Factory.StartNew(this.DownstreamLoop, TaskCreationOptions.LongRunning);
            var upstreamTask = Task.Factory.StartNew(this.UpstreamLoop, TaskCreationOptions.LongRunning);

            return Task.WhenAll(downstreamTask, upstreamTask);
        }

        async Task StopForwardingLoopsAsync()
        {
            this.downstreamPublications.ForEach(channel => channel.Writer.Complete());
            this.upstreamPublications.ForEach(channel => channel.Writer.Complete());

            await this.forwardingLoops.ForEachAsync(loop => loop);

            this.forwardingLoops = Option.None<Task>();
            this.downstreamPublications = Option.None<Channel<MqttPublishInfo>>();
            this.upstreamPublications = Option.None<Channel<MqttPublishInfo>>();
        }

        void CreateMessageChannels()
        {
            this.downstreamPublications = Option.Some(Channel.CreateUnbounded<MqttPublishInfo>(
                                            new UnboundedChannelOptions
                                            {
                                                SingleReader = true,
                                                SingleWriter = true
                                            }));

            this.upstreamPublications = Option.Some(Channel.CreateUnbounded<MqttPublishInfo>(
                                            new UnboundedChannelOptions
                                            {
                                                SingleReader = true,
                                                SingleWriter = true
                                            }));
        }

        async Task DownstreamLoop()
        {
            Events.DownstreamForwardingLoopStarted();

            var channel = this.downstreamPublications.Expect(() => new Exception("No downstream channel is prepared to read"));
            while (await channel.Reader.WaitToReadAsync())
            {
                var publishInfo = default(MqttPublishInfo);

                try
                {
                    publishInfo = await channel.Reader.ReadAsync();
                }
                catch (Exception e)
                {
                    Events.FailedToForwardDownstream(e);
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
                        Events.FailedToForwardDownstream(e);
                        // Keep going with other consumers...
                    }
                }

                if (!accepted)
                {
                    Events.MessageNotForwarded(publishInfo.Topic, publishInfo.Payload.Length);
                }
            }

            Events.DownstreamForwardingLoopStopped();
        }

        async Task UpstreamLoop()
        {
            var upstreamDispatcher = this.components.Consumers.Where(c => c is BrokeredCloudProxyDispatcher).FirstOrDefault();
            if (upstreamDispatcher == null)
            {
                throw new InvalidOperationException("There is no BrokeredCloudProxyDispatcher found in message consumer list");
            }

            Events.UpstreamForwardingLoopStarted();

            var channel = this.upstreamPublications.Expect(() => new Exception("No upstream channel is prepared to read"));
            while (await channel.Reader.WaitToReadAsync())
            {
                var publishInfo = default(MqttPublishInfo);

                try
                {
                    publishInfo = await channel.Reader.ReadAsync();

                    var accepted = await upstreamDispatcher.HandleAsync(publishInfo);
                    Events.MessageForwarded(upstreamDispatcher.GetType().Name, accepted, publishInfo.Topic, publishInfo.Payload.Length);
                }
                catch (Exception e)
                {
                    Events.FailedToForwardUpstream(e);
                    // keep going
                }
            }

            Events.UpstreamForwardingLoopStopped();
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
                        await acksArrived.WaitAsync(TimeSpan.FromSeconds(DefaultAckTimeoutSecs));
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
                DownstreamForwardingLoopStarted,
                DownstreamForwardingLoopStopped,
                UpstreamForwardingLoopStarted,
                UpstreamForwardingLoopStopped,
                FailedToForwardUpstream,
                FailedToForwardDownstream,
                MessageForwarded,
                MessageNotForwarded,
                FailedToForward,
                CouldNotConnect,
                TimeoutReceivingSubAcks,
                NoMqttClientWhenReconnecting
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
            public static void DownstreamForwardingLoopStarted() => Log.LogInformation((int)EventIds.DownstreamForwardingLoopStarted, "Downstream forwarding loop started");
            public static void DownstreamForwardingLoopStopped() => Log.LogInformation((int)EventIds.DownstreamForwardingLoopStopped, "Downstream forwarding loop stopped");
            public static void UpstreamForwardingLoopStarted() => Log.LogInformation((int)EventIds.UpstreamForwardingLoopStarted, "Upstream forwarding loop started");
            public static void UpstreamForwardingLoopStopped() => Log.LogInformation((int)EventIds.UpstreamForwardingLoopStopped, "Upstream forwarding loop stopped");
            public static void MessageForwarded(string consumer, bool accepted, string topic, int len) => Log.LogDebug((int)EventIds.MessageForwarded, "Message forwarded to {0} and it {1}. Topic {2}, Msg. len {3} bytes", consumer, accepted ? "accepted" : "ignored", topic, len);
            public static void MessageNotForwarded(string topic, int len) => Log.LogDebug((int)EventIds.MessageForwarded, "Message has not been forwarded to any consumers. Topic {0}, Msg. len {1} bytes", topic, len);
            public static void FailedToForwardUpstream(Exception e) => Log.LogError((int)EventIds.FailedToForwardUpstream, e, "Failed to forward message from upstream.");
            public static void FailedToForwardDownstream(Exception e) => Log.LogError((int)EventIds.FailedToForwardDownstream, e, "Failed to forward message from downstream.");
            public static void CouldNotConnect() => Log.LogInformation((int)EventIds.CouldNotConnect, "Could not connect to MQTT Broker, possibly it is not running. To disable MQTT Broker Connector, please set 'mqttBrokerSettings__enabled' environment variable to 'false'");
            public static void TimeoutReceivingSubAcks(Exception e) => Log.LogError((int)EventIds.TimeoutReceivingSubAcks, e, "MQTT Broker has not acknowledged subscriptions in time");
            public static void NoMqttClientWhenReconnecting() => Log.LogError((int)EventIds.NoMqttClientWhenReconnecting, "No Mqtt client instance when trying to reconnect - stopped trying.");
        }
    }
}
