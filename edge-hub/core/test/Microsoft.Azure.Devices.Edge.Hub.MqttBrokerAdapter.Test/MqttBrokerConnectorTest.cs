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
    using Castle.DynamicProxy.Generators.Emitters.SimpleAST;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Hub.Mqtt;
    using Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Org.BouncyCastle.Pkix;
    using Xunit;

    [Unit]
    public class MqttBrokerConnectorTest
    {
        [Fact]
        public void WhemStartedThenHooksUpToProducers()
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
            using var broker = new MiniMqttServer(4567);

            var sut = new ConnectorBuilder().Build();
            await sut.ConnectAsync("localhost", 4567);

            Assert.Equal(1, broker.ConnectionCounter);
        }

        [Fact]
        public async Task WhenStartedTwiceThenSecondFails()
        {
            using var broker = new MiniMqttServer(4567);

            var sut = new ConnectorBuilder().Build();
            await sut.ConnectAsync("localhost", 4567);

            Assert.Equal(1, broker.ConnectionCounter);

            await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ConnectAsync("localhost", 4567));

            Assert.Equal(1, broker.ConnectionCounter);
        }

        [Fact]
        public async Task WhenStartedThenSubscribesForConsumers()
        {
            using var broker = new MiniMqttServer(4567);

            var consumers = new[] {
                                    new ConsumerStub() { Subscriptions = new[] { "foo", "boo" } },
                                    new ConsumerStub() { Subscriptions = new[] { "moo", "shoo" } }
                                  };

            var sut = new ConnectorBuilder()
                            .WithConsumers(consumers)
                            .Build();

            await sut.ConnectAsync("localhost", 4567);

            var expected = consumers.SelectMany(c => c.Subscriptions).OrderBy(s => s);
            Assert.Equal(expected,  broker.Subscriptions.OrderBy(s => s));
        }

        [Fact]
        public async Task WhenMessageReceivedThenForwardsToConsumers()
        {
            using var broker = new MiniMqttServer(4567);

            var milestone = new SemaphoreSlim(0, 2);
            var callCounter = 0;
            var consumers = new[] {
                                    new ConsumerStub() { ShouldHandle = true, Handler = _ => milestone.Release()},
                                    new ConsumerStub() { ShouldHandle = true, Handler = _ => Interlocked.Increment(ref callCounter)}
                                  };

            var sut = new ConnectorBuilder()
                .WithConsumers(consumers)
                .Build();

            await sut.ConnectAsync("localhost", 4567);

            await broker.PublishAsync("boo", Encoding.ASCII.GetBytes("hoo"));
            await milestone.WaitAsync();

            // publish again, so if the first message was going to sent to the second subscriber
            // it would not be missed
            await broker.PublishAsync("boo", Encoding.ASCII.GetBytes("hoo"));
            await milestone.WaitAsync();

            Assert.Equal(2, consumers[0].PacketsToHandle.Count());
            Assert.Equal(0, Volatile.Read(ref callCounter));
        }

        // FIXME a test that checks the when disconnects, the pending acks (for TCSs) gets cancelled
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

        public MiniMqttServer(int port)
        {
            try
            {
                this.Subscriptions = new List<string>();

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
    }
}
