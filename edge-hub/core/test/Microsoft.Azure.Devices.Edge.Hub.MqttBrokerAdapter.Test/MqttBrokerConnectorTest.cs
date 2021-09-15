// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.NetworkInformation;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Integration]
    public class MqttBrokerConnectorTest
    {
        const string HOST = "localhost";

        [Fact]
        public void WhenStartedThenHooksUpToProducers()
        {
            var producers = new[] { new ProducerStub(), new ProducerStub() };
            var sut = new ConnectorBuilder()
                            .WithProducers(producers)
                            .Build();

            Assert.All(producers, p => Assert.Equal(sut, p.Connector));
        }

        [Fact]
        public async Task WhenStartedThenConnectsToServer()
        {
            using var broker = new MiniMqttServer();
            using var sut = new ConnectorBuilder().Build();

            await sut.ConnectAsync(HOST, broker.Port);

            Assert.Equal(1, broker.ConnectionCounter);
        }

        [Fact]
        public async Task WhenStartedTwiceThenSecondFails()
        {
            using var broker = new MiniMqttServer();
            using var sut = new ConnectorBuilder().Build();

            await sut.ConnectAsync(HOST, broker.Port);

            Assert.Equal(1, broker.ConnectionCounter);

            await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ConnectAsync(HOST, broker.Port));

            Assert.Equal(1, broker.ConnectionCounter);
        }

        [Fact]
        public async Task WhenStartedThenSubscribesForConsumers()
        {
            using var broker = new MiniMqttServer();

            var consumers = new[]
            {
                new ConsumerStub { Subscriptions = new[] { "foo", "boo" } },
                new ConsumerStub { Subscriptions = new[] { "moo", "shoo" } }
            };

            using var sut = new ConnectorBuilder()
                .WithConsumers(consumers)
                .Build();

            await sut.ConnectAsync(HOST, broker.Port);

            var expected = consumers.SelectMany(c => c.Subscriptions).OrderBy(s => s);
            Assert.Equal(expected, broker.Subscriptions.OrderBy(s => s));
        }

        [Fact]
        public async Task WhenMessageReceivedThenForwardsToConsumers()
        {
            using var broker = new MiniMqttServer();

            var milestone = new SemaphoreSlim(0, 2);
            var consumers = new[]
            {
                new ConsumerStub { ShouldHandle = false, Handler = _ => milestone.Release() },
                new ConsumerStub { ShouldHandle = false, Handler = _ => milestone.Release() }
            };

            using var sut = new ConnectorBuilder()
                .WithConsumers(consumers)
                .Build();

            await sut.ConnectAsync(HOST, broker.Port);

            await broker.PublishAsync("boo", Encoding.ASCII.GetBytes("hoo"));

            Assert.True(await milestone.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.True(await milestone.WaitAsync(TimeSpan.FromSeconds(5)));

            Assert.All(consumers, c => Assert.Single(c.PacketsToHandle));
            Assert.All(consumers, c => Assert.Equal("boo", c.PacketsToHandle.First().Topic));
            Assert.All(consumers, c => Assert.Equal("hoo", Encoding.ASCII.GetString(c.PacketsToHandle.First().Payload)));
        }

        [Fact]
        public async Task WhenMessageHandledThenForwardingLoopBreaks()
        {
            using var broker = new MiniMqttServer();

            var milestone = new SemaphoreSlim(0, 2);
            var callCounter = 0;
            var consumers = new[]
            {
                new ConsumerStub { ShouldHandle = true, Handler = _ => milestone.Release() },
                new ConsumerStub { ShouldHandle = true, Handler = _ => Interlocked.Increment(ref callCounter) }
            };

            using var sut = new ConnectorBuilder()
                .WithConsumers(consumers)
                .Build();

            await sut.ConnectAsync(HOST, broker.Port);

            await broker.PublishAsync("boo", Encoding.ASCII.GetBytes("hoo"));
            Assert.True(await milestone.WaitAsync(TimeSpan.FromSeconds(5)));

            // publish again, so if the first message was going to sent to the second subscriber
            // it would not be missed
            await broker.PublishAsync("boo", Encoding.ASCII.GetBytes("hoo"));
            Assert.True(await milestone.WaitAsync(TimeSpan.FromSeconds(5)));

            Assert.Equal(2, consumers[0].PacketsToHandle.Count());
            Assert.Equal(0, Volatile.Read(ref callCounter));
        }

        [Fact]
        public async Task WhenConsumerThrowsThenProcessingLoopContinues()
        {
            using var broker = new MiniMqttServer();

            var milestone = new SemaphoreSlim(0, 1);
            var consumers = new[]
            {
                new ConsumerStub { ShouldHandle = true, Handler = _ => throw new Exception() },
                new ConsumerStub { ShouldHandle = true, Handler = _ => milestone.Release() }
            };

            using var sut = new ConnectorBuilder()
                .WithConsumers(consumers)
                .Build();

            await sut.ConnectAsync(HOST, broker.Port);

            await broker.PublishAsync("boo", Encoding.ASCII.GetBytes("hoo"));

            Assert.True(await milestone.WaitAsync(TimeSpan.FromSeconds(5)));

            Assert.Single(consumers[1].PacketsToHandle);
            Assert.Equal("boo", consumers[1].PacketsToHandle.First().Topic);
            Assert.Equal("hoo", Encoding.ASCII.GetString(consumers[1].PacketsToHandle.First().Payload));
        }

        [Fact]
        public async Task ProducersCanSendMessages()
        {
            using var broker = new MiniMqttServer();

            var milestone = new SemaphoreSlim(0, 1);
            broker.OnPublish = () => milestone.Release();

            var producer = new ProducerStub();

            using var sut = new ConnectorBuilder()
                .WithProducer(producer)
                .Build();

            await sut.ConnectAsync(HOST, broker.Port);
            await producer.Connector.SendAsync("boo", Encoding.ASCII.GetBytes("hoo"));

            Assert.True(await milestone.WaitAsync(TimeSpan.FromSeconds(5)));

            Assert.Single(broker.Publications);
            Assert.Equal("boo", broker.Publications.First().Item1);
            Assert.Equal("hoo", Encoding.ASCII.GetString(broker.Publications.First().Item2));
        }

        [Fact]
        public async Task ProducersWaitForAck()
        {
            using var broker = new MiniMqttServer();

            var milestone1 = new SemaphoreSlim(0, 1);
            var milestone2 = new SemaphoreSlim(0, 1);
            broker.OnPublish =
                () =>
                {
                    milestone1.Release();
                    milestone2.Wait(TimeSpan.FromSeconds(5));
                };

            var producer = new ProducerStub();

            using var sut = new ConnectorBuilder()
                .WithProducer(producer)
                .Build();

            await sut.ConnectAsync(HOST, broker.Port);
            var senderTask = producer.Connector.SendAsync("boo", Encoding.ASCII.GetBytes("hoo"));

            Assert.True(await milestone1.WaitAsync(TimeSpan.FromSeconds(5)));

            // Holding back ACK through milestone2
            await Task.Delay(TimeSpan.FromSeconds(2));
            Assert.False(senderTask.IsCompleted);
            milestone2.Release();

            await senderTask;

            Assert.Single(broker.Publications);
            Assert.Equal("boo", broker.Publications.First().Item1);
            Assert.Equal("hoo", Encoding.ASCII.GetString(broker.Publications.First().Item2));
        }

        [Fact]
        public async Task SendAsyncCancelsWhenDisconnecting()
        {
            using var broker = new MiniMqttServer();
            var sut = default(MqttBrokerConnector);

            broker.OnPublish =
                () =>
                {
                    sut.DisconnectAsync().Wait();
                    throw new Exception(); // this stops sending ACK back
                };

            var producer = new ProducerStub();

            sut = new ConnectorBuilder()
                .WithProducer(producer)
                .Build();

            await sut.ConnectAsync(HOST, broker.Port);
            await Assert.ThrowsAsync<TaskCanceledException>(async () => await producer.Connector.SendAsync("boo", Encoding.ASCII.GetBytes("hoo")));
        }

        [Fact]
        public async Task SendAsyncCancelsWhenTimeout()
        {
            using var broker = new MiniMqttServer();
            var sut = default(MqttBrokerConnector);

            broker.OnPublish =
                () =>
                {
                    throw new Exception(); // this stops sending ACK back
                };

            var producer = new ProducerStub();

            sut = new ConnectorBuilder()
                .WithProducer(producer)
                .Build();

            sut.AckTimeout = TimeSpan.FromSeconds(2);

            await sut.ConnectAsync(HOST, broker.Port);
            await Assert.ThrowsAsync<TimeoutException>(async () => await producer.Connector.SendAsync("boo", Encoding.ASCII.GetBytes("hoo")));
        }

        [Fact]
        public async Task TriesReconnect()
        {
            using var broker = new MiniMqttServer();
            using var sut = new ConnectorBuilder().Build();

            await sut.ConnectAsync(HOST, broker.Port);

            Assert.Equal(1, broker.ConnectionCounter);

            var milestone = new SemaphoreSlim(0, 1);
            broker.OnConnect = () => milestone.Release();
            broker.DropActiveClient();

            Assert.True(await milestone.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Equal(2, broker.ConnectionCounter);
        }

        [Fact(Skip = "Flaky")]
        public async Task WhenReconnectsThenResubscribes()
        {
            using var broker = new MiniMqttServer();

            var consumers = new[]
            {
                new ConsumerStub { Subscriptions = new[] { "foo", "boo" } },
                new ConsumerStub { Subscriptions = new[] { "moo", "shoo" } }
            };

            using var sut = new ConnectorBuilder()
                .WithConsumers(consumers)
                .Build();

            await sut.ConnectAsync(HOST, broker.Port);

            Assert.Equal(1, broker.ConnectionCounter);

            broker.Subscriptions.Clear();

            var milestoneConnected = new SemaphoreSlim(0, 1);
            broker.OnConnect = () => milestoneConnected.Release();

            var milestoneSubscribed = new SemaphoreSlim(0, 1);
            var subscriptionCount = consumers.SelectMany(c => c.Subscriptions).Count();
            broker.OnSubscribe = () =>
            {
                if (Interlocked.Decrement(ref subscriptionCount) == 0)
                {
                    milestoneSubscribed.Release();
                }
            };

            broker.DropActiveClient();

            Assert.True(await milestoneConnected.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.True(await milestoneSubscribed.WaitAsync(TimeSpan.FromSeconds(5)));

            var expected = consumers.SelectMany(c => c.Subscriptions).OrderBy(s => s).ToArray();
            var actual = broker.Subscriptions.OrderBy(s => s).ToArray();

            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task OfflineSendGetSentAfterReconnect()
        {
            var producer = new ProducerStub();

            using var sut = new ConnectorBuilder()
                .WithProducer(producer)
                .Build();

            var busyPorts = IPGlobalProperties.GetIPGlobalProperties()
                .GetActiveTcpListeners()
                .Select(endpoint => endpoint.Port)
                .ToList();

            var port = Enumerable.Range(10883, ushort.MaxValue).First(port => !busyPorts.Contains(port));

            using (var broker = new MiniMqttServer(port))
            {
                await sut.ConnectAsync(HOST, broker.Port);

                Assert.Equal(1, broker.ConnectionCounter);

                broker.DropActiveClient();
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200)); // give time to settle with dropped connection

            var sendTask = producer.Connector.SendAsync("boo", Encoding.ASCII.GetBytes("hoo"));
            await Task.Delay(TimeSpan.FromSeconds(3)); // let the connector work a bit on reconnect

            using (var broker = new MiniMqttServer(port))
            {
                await sendTask;

                Assert.Single(broker.Publications);
                Assert.Equal("boo", broker.Publications.First().Item1);
                Assert.Equal("hoo", Encoding.ASCII.GetString(broker.Publications.First().Item2));
            }
        }

        [Fact]
        public async Task MessagesFromUpstreamHandledOnSeparatePath()
        {
            using var broker = new MiniMqttServer();

            var upstreamMilestone = new SemaphoreSlim(0, 1);
            var downstreamMilestone = new SemaphoreSlim(0, 1);

            var edgeHubStub = new EdgeHubStub(() => upstreamMilestone.Release());

            var upstreamDispatcher = new BrokeredCloudProxyDispatcher();
            upstreamDispatcher.BindEdgeHub(edgeHubStub);

            var consumers = new IMessageConsumer[]
            {
                new ConsumerStub
                    {
                        ShouldHandle = true,
                        Handler = _ =>
                        {
                            upstreamMilestone.Wait(); // this blocks the pump for downstream messages
                            downstreamMilestone.Release();
                        }
                    },

                upstreamDispatcher
            };

            using var sut = new ConnectorBuilder()
                                    .WithConsumers(consumers)
                                    .Build();

            await sut.ConnectAsync(HOST, broker.Port);

            // handled by downstream pump and the pump gets blocked
            await broker.PublishAsync("boo", Encoding.ASCII.GetBytes("hoo"));

            // handled by upstream pump that let's downstream pump getting unblocked
            await broker.PublishAsync("$downstream/device_1/methods/post/foo/?$rid=123", new byte[0]);

            // check if downsteam pump got unblocked
            Assert.True(await downstreamMilestone.WaitAsync(TimeSpan.FromSeconds(5)));
        }
    }

    class ProducerStub : IMessageProducer
    {
        public IMqttBrokerConnector Connector { get; set; }
        public void SetConnector(IMqttBrokerConnector connector) => this.Connector = connector;
    }

    class ConsumerStub : IMessageConsumer
    {
        public IReadOnlyCollection<string> Subscriptions { get; set; } = new List<string>();
        public List<MqttPublishInfo> PacketsToHandle { get; set; } = new List<MqttPublishInfo>();
        public bool ShouldHandle { get; set; }
        public Action<MqttPublishInfo> Handler { get; set; } = p => { };

        public Task<bool> HandleAsync(MqttPublishInfo publishInfo)
        {
            this.PacketsToHandle.Add(publishInfo);
            this.Handler(publishInfo);
            return Task.FromResult(this.ShouldHandle);
        }
    }

    class DisposableMqttBrokerConnector : MqttBrokerConnector, IDisposable
    {
        public DisposableMqttBrokerConnector(IComponentDiscovery components, ISystemComponentIdProvider systemComponentIdProvider)
            : base(components, systemComponentIdProvider)
        {
        }

        public void Dispose() => this.DisconnectAsync().Wait();
    }

    class ConnectorBuilder
    {
        List<IMessageProducer> producers = new List<IMessageProducer>();
        List<IMessageConsumer> consumers = new List<IMessageConsumer>();

        public ConnectorBuilder WithProducer(IMessageProducer producer)
        {
            this.producers.Add(producer);
            return this;
        }

        public ConnectorBuilder WithProducers(IReadOnlyCollection<IMessageProducer> producers)
        {
            foreach (var producer in producers)
            {
                this.producers.Add(producer);
            }

            return this;
        }

        public ConnectorBuilder WithConsumer(IMessageConsumer consumer)
        {
            this.consumers.Add(consumer);
            return this;
        }

        public ConnectorBuilder WithConsumers(IReadOnlyCollection<IMessageConsumer> consumers)
        {
            foreach (var consumer in consumers)
            {
                this.consumers.Add(consumer);
            }

            return this;
        }

        public DisposableMqttBrokerConnector Build()
        {
            var componentDiscovery = Mock.Of<IComponentDiscovery>();
            Mock.Get(componentDiscovery).SetupGet(c => c.Producers).Returns(producers);
            Mock.Get(componentDiscovery).SetupGet(c => c.Consumers).Returns(consumers);

            var systemComponentIdProvider = Mock.Of<ISystemComponentIdProvider>();
            Mock.Get(systemComponentIdProvider).SetupGet(s => s.EdgeHubBridgeId).Returns("some_id");

            return new DisposableMqttBrokerConnector(componentDiscovery, systemComponentIdProvider);
        }
    }

    class EdgeHubStub : IEdgeHub
    {
        Action whenCalled;

        public EdgeHubStub(Action whenCalled)
        {
            this.whenCalled = whenCalled;
        }

        public void Dispose()
        {
        }

        public IDeviceScopeIdentitiesCache GetDeviceScopeIdentitiesCache() => throw new NotImplementedException();
        public string GetEdgeDeviceId() => "x";

        public Task<IMessage> GetTwinAsync(string id)
        {
            this.whenCalled();
            return Task.FromResult(new EdgeMessage(new byte[0], new Dictionary<string, string>(), new Dictionary<string, string>()) as IMessage);
        }

        public Task<DirectMethodResponse> InvokeMethodAsync(string id, DirectMethodRequest methodRequest)
        {
            this.whenCalled();
            return Task.FromResult(new DirectMethodResponse("boo", new byte[0], 200));
        }

        public Task ProcessDeviceMessage(IIdentity identity, IMessage message) => WhenCalled();
        public Task ProcessDeviceMessageBatch(IIdentity identity, IEnumerable<IMessage> message) => WhenCalled();
        public Task AddSubscription(string id, DeviceSubscription deviceSubscription) => WhenCalled();
        public Task ProcessSubscriptions(string id, IEnumerable<(DeviceSubscription, bool)> subscriptions) => WhenCalled();
        public Task RemoveSubscription(string id, DeviceSubscription deviceSubscription) => WhenCalled();
        public Task RemoveSubscriptions(string id) => WhenCalled();
        public Task SendC2DMessageAsync(string id, IMessage message) => WhenCalled();
        public Task UpdateDesiredPropertiesAsync(string id, IMessage twinCollection) => WhenCalled();
        public Task UpdateReportedPropertiesAsync(IIdentity identity, IMessage reportedPropertiesMessage) => WhenCalled();

        Task WhenCalled()
        {
            this.whenCalled();
            return Task.CompletedTask;
        }
    }

    class MiniMqttServer : IDisposable
    {
        CancellationTokenSource cts;

        TcpListener listener;
        Task processingTask;

        NetworkStream lastClient;

        int nextPacketId = 1;

        public int ConnectionCounter { get; private set; }
        public List<string> Subscriptions { get; private set; }
        public List<(string, byte[])> Publications { get; private set; }

        public Action OnPublish { private get; set; }
        public Action OnSubscribe { private get; set; }
        public Action OnConnect { private get; set; }

        public MiniMqttServer(int? port = null)
        {
            try
            {
                this.Subscriptions = new List<string>();
                this.Publications = new List<(string, byte[])>();

                this.OnPublish = () => { };
                this.OnSubscribe = () => { };
                this.OnConnect = () => { };

                this.listener = TcpListener.Create(port.GetValueOrDefault());
                this.listener.Start();

                this.cts = new CancellationTokenSource();
                processingTask = ProcessingLoop(listener, this.cts.Token);
            }
            catch (Exception e)
            {
                throw new Exception("Could not start MiniMqttServer", e);
            }
        }

        public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;

        public async Task PublishAsync(string topic, byte[] payload)
        {
            var packet = CreatePacket(topic, payload);
            await this.lastClient.WriteAsync(packet, 0, packet.Length);
        }

        public void DropActiveClient()
        {
            try
            {
                lastClient?.Close();
            }
            catch { }
        }

        public void Dispose()
        {
            cts.Cancel();
            this.listener.Stop();
            DropActiveClient();
            this.processingTask.Wait();
        }

        async Task ProcessingLoop(TcpListener listener, CancellationToken token)
        {
            var hasStopped = false;
            while (!(hasStopped || token.IsCancellationRequested))
            {
                try
                {
                    var newClient = await listener.AcceptTcpClientAsync();
                    _ = ProcessClient(newClient, token);
                }
                catch
                {
                    hasStopped = true;
                }
            }
        }

        async Task ProcessClient(TcpClient client, CancellationToken token)
        {
            var clientStream = client.GetStream();
            this.lastClient = clientStream; // so Publish() has something to work with

            do
            {
                if (token.IsCancellationRequested)
                {
                    break;
                }

                var firstTwoBytes = await ReadBytesAsync(clientStream, 2);
                if (!firstTwoBytes.HasValue)
                {
                    break;
                }

                var (type, size) = firstTwoBytes.Map(h => (h[0], h[1]))
                    .Expect(() => new InvalidOperationException("mqtt header"));

                var packet = await ReadBytesAsync(clientStream, size);
                if (!packet.HasValue)
                {
                    break;
                }

                var content = packet.Expect(() => new InvalidOperationException("mqtt packet"));
                switch (type)
                {
                    case 0x10:
                        await this.HandleConnect(clientStream);
                        break;

                    case 0x82:
                        await this.HandleSubscription(clientStream, content);
                        break;

                    case 0x32:
                        await this.HandlePublish(clientStream, content);
                        break;
                }
            }
            while (true);
        }

        byte[] CreatePacket(string topic, byte[] payload)
        {
            var packetSize = 1 + 1 + 2 + topic.Length + 2 + payload.Length;
            var result = new byte[packetSize];

            var pos = 0;
            result[pos++] = 0x32;
            result[pos++] = (byte)(2 + topic.Length + 2 + payload.Length);
            result[pos++] = (byte)(topic.Length >> 8);
            result[pos++] = (byte)(topic.Length & 0xff);

            Array.Copy(Encoding.ASCII.GetBytes(topic), 0, result, pos, topic.Length);
            pos += topic.Length;

            result[pos++] = (byte)(this.nextPacketId >> 8);
            result[pos++] = (byte)(this.nextPacketId & 0xff);

            Array.Copy(payload, 0, result, pos, payload.Length);

            this.nextPacketId++;

            return result;
        }

        static async Task<Option<byte[]>> ReadBytesAsync(NetworkStream stream, int count)
        {
            var result = new byte[count];
            var toRead = count;
            var totalRead = 0;

            while (toRead > 0)
            {
                var readBytes = await stream.ReadAsync(result, totalRead, toRead);

                // assume the connection was closed by the peer when can read only 0 bytes
                if (readBytes == 0)
                {
                    return Option.None<byte[]>();
                }

                totalRead += readBytes;
                toRead -= readBytes;
            }

            return Option.Some(result);
        }

        async Task HandleConnect(NetworkStream stream)
        {
            this.ConnectionCounter++;

            this.OnConnect();

            var conack = new byte[] { 0x20, 0x02, 0x00, 0x00 };
            await stream.WriteAsync(conack);
        }

        async Task HandleSubscription(NetworkStream stream, byte[] content)
        {
            var idHi = content[0];
            var idLo = content[1];
            var remaining = content.Length - 2;
            var currentPos = 2;
            var topicCnt = 0;

            while (remaining > 0)
            {
                topicCnt++;

                var len = (content[currentPos] << 8) + content[currentPos + 1];

                var newSubscription = Encoding.UTF8.GetString(content, currentPos + 2, len);
                this.Subscriptions.Add(newSubscription);
                this.OnSubscribe();

                currentPos += 2 + len + 1; // +1 for QoS
                remaining -= 2 + len + 1;
            }

            var response = new byte[4 + topicCnt];

            response[0] = 0x90;
            response[1] = (byte)(2 + topicCnt);
            response[2] = idHi;
            response[3] = idLo;

            for (var i = 0; i < topicCnt; i++)
            {
                response[4 + i] = 1;
            }

            await stream.WriteAsync(response, 0, response.Length);
        }

        async Task HandlePublish(NetworkStream stream, byte[] content)
        {
            var topicLen = (content[0] << 8) + content[1];
            var topic = Encoding.UTF8.GetString(content, 2, topicLen);

            var idHi = content[2 + topicLen];
            var idLo = content[2 + topicLen + 1];

            var payload = new byte[content.Length - 2 - topicLen - 2];
            Array.Copy(content, 2 + topicLen + 2, payload, 0, payload.Length);

            this.Publications.Add((topic, payload));

            try
            {
                this.OnPublish();
            }
            catch
            {
                // so we can simulate cases when no ack was sent
                return;
            }

            var ack = new byte[4];
            ack[0] = 0x40;
            ack[1] = 0x02;
            ack[2] = idHi;
            ack[3] = idLo;

            await stream.WriteAsync(ack, 0, ack.Length);
        }
    }
}
