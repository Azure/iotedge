// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Integration]
    public class MqttBrokerConnectorTest
    {
        const string HOST = "localhost";        
        const int PORT = 4567;

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
            using var broker = new MiniMqttServer(PORT);

            var sut = new ConnectorBuilder().Build();
            await sut.ConnectAsync(HOST, PORT);

            Assert.Equal(1, broker.ConnectionCounter);
        }

        [Fact]
        public async Task WhenStartedTwiceThenSecondFails()
        {
            using var broker = new MiniMqttServer(PORT);

            var sut = new ConnectorBuilder().Build();
            await sut.ConnectAsync(HOST, PORT);

            Assert.Equal(1, broker.ConnectionCounter);

            await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ConnectAsync(HOST, PORT));

            Assert.Equal(1, broker.ConnectionCounter);
        }

        [Fact]
        public async Task WhenStartedThenSubscribesForConsumers()
        {
            using var broker = new MiniMqttServer(PORT);

            var consumers = new[] {
                                    new ConsumerStub() { Subscriptions = new[] { "foo", "boo" } },
                                    new ConsumerStub() { Subscriptions = new[] { "moo", "shoo" } }
                                  };

            var sut = new ConnectorBuilder()
                            .WithConsumers(consumers)
                            .Build();

            await sut.ConnectAsync(HOST, PORT);

            var expected = consumers.SelectMany(c => c.Subscriptions).OrderBy(s => s);
            Assert.Equal(expected,  broker.Subscriptions.OrderBy(s => s));
        }

        [Fact]
        public async Task WhenMessageReceivedThenForwardsToConsumers()
        {
            using var broker = new MiniMqttServer(PORT);

            var milestone = new SemaphoreSlim(0, 2);
            var consumers = new[] {
                                    new ConsumerStub() { ShouldHandle = false, Handler = _ => milestone.Release()},
                                    new ConsumerStub() { ShouldHandle = false, Handler = _ => milestone.Release()}
                                  };

            var sut = new ConnectorBuilder()
                            .WithConsumers(consumers)
                            .Build();

            await sut.ConnectAsync(HOST, PORT);

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
            using var broker = new MiniMqttServer(PORT);

            var milestone = new SemaphoreSlim(0, 2);
            var callCounter = 0;
            var consumers = new[] {
                                    new ConsumerStub() { ShouldHandle = true, Handler = _ => milestone.Release()},
                                    new ConsumerStub() { ShouldHandle = true, Handler = _ => Interlocked.Increment(ref callCounter)}
                                  };

            var sut = new ConnectorBuilder()
                            .WithConsumers(consumers)
                            .Build();

            await sut.ConnectAsync(HOST, PORT);

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
            using var broker = new MiniMqttServer(PORT);

            var milestone = new SemaphoreSlim(0, 1);
            var consumers = new[] {
                                    new ConsumerStub() { ShouldHandle = true, Handler = _ => throw new Exception()},
                                    new ConsumerStub() { ShouldHandle = true, Handler = _ => milestone.Release()}
                                  };

            var sut = new ConnectorBuilder()
                            .WithConsumers(consumers)
                            .Build();

            await sut.ConnectAsync(HOST, PORT);

            await broker.PublishAsync("boo", Encoding.ASCII.GetBytes("hoo"));

            Assert.True(await milestone.WaitAsync(TimeSpan.FromSeconds(5)));

            Assert.Single(consumers[1].PacketsToHandle);
            Assert.Equal("boo", consumers[1].PacketsToHandle.First().Topic);
            Assert.Equal("hoo", Encoding.ASCII.GetString(consumers[1].PacketsToHandle.First().Payload));
        }

        [Fact]
        public async Task ProducersCanSendMessages()
        {
            using var broker = new MiniMqttServer(PORT);

            var milestone = new SemaphoreSlim(0, 1);
            broker.OnPublish = () => milestone.Release();

            var producer = new ProducerStub();

            var sut = new ConnectorBuilder()
                            .WithProducer(producer)
                            .Build();

            await sut.ConnectAsync(HOST, PORT);
            await producer.Connector.SendAsync("boo", Encoding.ASCII.GetBytes("hoo"));

            Assert.True(await milestone.WaitAsync(TimeSpan.FromSeconds(5)));

            Assert.Single(broker.Publications);
            Assert.Equal("boo", broker.Publications.First().Item1);
            Assert.Equal("hoo", Encoding.ASCII.GetString(broker.Publications.First().Item2));
        }

        [Fact]
        public async Task ProducersWaitForAck()
        {
            using var broker = new MiniMqttServer(PORT);

            var milestone1 = new SemaphoreSlim(0, 1);
            var milestone2 = new SemaphoreSlim(0, 1);
            broker.OnPublish =
                () =>
                {
                    milestone1.Release();
                    milestone2.Wait(TimeSpan.FromSeconds(5));
                };

            var producer = new ProducerStub();

            var sut = new ConnectorBuilder()
                            .WithProducer(producer)
                            .Build();

            await sut.ConnectAsync(HOST, PORT);
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
            using var broker = new MiniMqttServer(PORT);
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

            await sut.ConnectAsync(HOST, PORT);
            await Assert.ThrowsAsync<TaskCanceledException>(async () => await producer.Connector.SendAsync("boo", Encoding.ASCII.GetBytes("hoo")));
        }

        [Fact]
        public async Task TriesReconnect()
        {
            using var broker = new MiniMqttServer(PORT);

            var sut = new ConnectorBuilder().Build();
            await sut.ConnectAsync(HOST, PORT);

            Assert.Equal(1, broker.ConnectionCounter);

            var milestone = new SemaphoreSlim(0, 1);
            broker.OnConnect = () => milestone.Release();
            broker.DropActiveClient();

            Assert.True(await milestone.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Equal(2, broker.ConnectionCounter);
        }

        [Fact]
        public async Task WhenReconnectsThenResubscribes()
        {
            using var broker = new MiniMqttServer(PORT);

            var consumers = new[] {
                                    new ConsumerStub() { Subscriptions = new[] { "foo", "boo" } },
                                    new ConsumerStub() { Subscriptions = new[] { "moo", "shoo" } }
                                  };

            var sut = new ConnectorBuilder()
                            .WithConsumers(consumers)
                            .Build();

            await sut.ConnectAsync(HOST, PORT);

            Assert.Equal(1, broker.ConnectionCounter);

            broker.Subscriptions.Clear();

            var milestone = new SemaphoreSlim(0, 1);
            broker.OnConnect = () => milestone.Release();
            broker.DropActiveClient();

            Assert.True(await milestone.WaitAsync(TimeSpan.FromSeconds(5)));

            var expected = consumers.SelectMany(c => c.Subscriptions).OrderBy(s => s);
            Assert.Equal(expected, broker.Subscriptions.OrderBy(s => s));
        }
    }

    class ProducerStub : IMessageProducer
    {
        public IMqttBrokerConnector Connector { get; set; }

        public void SetConnector(IMqttBrokerConnector connector)
        {
            this.Connector = connector;
        }
    }

    class ConsumerStub : IMessageConsumer
    {
        public IReadOnlyCollection<string> Subscriptions { get; set; } = new List<string>();
        public List<MqttPublishInfo> PacketsToHandle { get; set; } = new List<MqttPublishInfo>();
        public bool ShouldHandle { get;  set; }
        public Action<MqttPublishInfo> Handler { get; set; } = p => { };

        public Task<bool> HandleAsync(MqttPublishInfo publishInfo)
        {
            this.PacketsToHandle.Add(publishInfo);
            this.Handler(publishInfo);
            return Task.FromResult(this.ShouldHandle);
        }

        public void ProducerStopped()
        {
            return;
        }
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

        public MqttBrokerConnector Build()
        {
            var componentDiscovery = Mock.Of<IComponentDiscovery>();
            Mock.Get(componentDiscovery).SetupGet(c => c.Producers).Returns(producers);
            Mock.Get(componentDiscovery).SetupGet(c => c.Consumers).Returns(consumers);

            var systemComponentIdProvider = Mock.Of<ISystemComponentIdProvider>();
            Mock.Get(systemComponentIdProvider).SetupGet(s => s.EdgeHubBridgeId).Returns("some_id");

            return new MqttBrokerConnector(componentDiscovery, systemComponentIdProvider);
        }
    }

    class MiniMqttServer : IDisposable
    {
        TcpListener listener;
        Task processingTask;

        NetworkStream lastClient;

        int nextPacketId = 1;

        public int ConnectionCounter { get; private set; }
        public List<string> Subscriptions { get; private set; }
        public List<(string, byte[])> Publications { get; private set; }

        public Action OnPublish { private get; set; }
        public Action OnConnect { private get; set; }

        public MiniMqttServer(int port)
        {
            try
            {
                this.Subscriptions = new List<string>();
                this.Publications = new List<(string, byte[])>();

                this.OnPublish = () => { };
                this.OnConnect = () => { };

                this.listener = TcpListener.Create(port);
                this.listener.Start();

                processingTask = ProcessingLoop(listener);
            }
            catch (Exception e)
            {
                throw new Exception("Could not start MiniMqttServer", e);
            }            
        }

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
            this.listener.Stop();
            this.processingTask.Wait();
        }

        async Task ProcessingLoop(TcpListener listener)
        {
            var hasStopped = false;
            do
            {
                try
                {
                    var newClient = await listener.AcceptTcpClientAsync();
                    _ = ProcessClient(newClient);
                }
                catch
                {
                    hasStopped = true;
                }

            }
            while (!hasStopped);
        }

        async Task ProcessClient(TcpClient client)
        {
            var clientStream = client.GetStream();
            this.lastClient = clientStream; // so Publish() has something to work with

            do
            {
                var firstTwoBytes = await ReadBytesAsync(clientStream, 2);

                var type = firstTwoBytes[0];
                var size = firstTwoBytes[1];

                var content = await ReadBytesAsync(clientStream, size);

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

        static async Task<byte []> ReadBytesAsync(NetworkStream stream, int count)
        {
            var result = new byte[count];
            var toRead = count;
            var totalRead = 0;

            while (toRead > 0)
            {
                var readBytes = await stream.ReadAsync(result, totalRead, toRead);

                totalRead += readBytes;
                toRead -= readBytes;
            }

            return result;
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
