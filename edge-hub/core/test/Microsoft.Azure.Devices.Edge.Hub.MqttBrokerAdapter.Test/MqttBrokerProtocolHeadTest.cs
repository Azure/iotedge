// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter.Test
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Unit]
    public class MqttBrokerProtocolHeadTest
    {
        [Fact]
        public async Task StartsConnector()
        {
            var config = new MqttBrokerProtocolHeadConfig(8883, "localhost");
            var connector = Mock.Of<IMqttBrokerConnector>();

            Mock.Get(connector).Setup(c => c.ConnectAsync(It.IsAny<String>(), It.IsAny<int>())).Returns(Task.CompletedTask);

            var sut = new MqttBrokerProtocolHead(config, connector);

            await sut.StartAsync();

            Mock.Get(connector).VerifyAll();
        }

        [Fact]
        public async Task DoesNotStartTwice()
        {
            var config = new MqttBrokerProtocolHeadConfig(8883, "localhost");
            var connector = Mock.Of<IMqttBrokerConnector>();

            Mock.Get(connector).Setup(c => c.ConnectAsync(It.IsAny<String>(), It.IsAny<int>())).Returns(Task.CompletedTask);

            var sut = new MqttBrokerProtocolHead(config, connector);

            await sut.StartAsync();
            await sut.StartAsync();

            Mock.Get(connector).Verify(c => c.ConnectAsync(It.IsAny<String>(), It.IsAny<int>()), Times.Once);
        }

        [Fact]
        public async Task NotStartedDoesNotStopConnector()
        {
            var config = new MqttBrokerProtocolHeadConfig(8883, "localhost");
            var connector = Mock.Of<IMqttBrokerConnector>();

            Mock.Get(connector).Setup(c => c.DisconnectAsync()).Returns(Task.CompletedTask);

            var sut = new MqttBrokerProtocolHead(config, connector);

            await sut.CloseAsync(CancellationToken.None);

            Mock.Get(connector).Verify(c => c.ConnectAsync(It.IsAny<String>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task StopsConnector()
        {
            var config = new MqttBrokerProtocolHeadConfig(8883, "localhost");
            var connector = Mock.Of<IMqttBrokerConnector>();

            Mock.Get(connector).Setup(c => c.ConnectAsync(It.IsAny<String>(), It.IsAny<int>())).Returns(Task.CompletedTask);
            Mock.Get(connector).Setup(c => c.DisconnectAsync()).Returns(Task.CompletedTask);

            var sut = new MqttBrokerProtocolHead(config, connector);

            await sut.StartAsync();
            await sut.CloseAsync(CancellationToken.None);

            Mock.Get(connector).VerifyAll();
        }

        [Fact]
        public async Task DoesNotDisconnectTwice()
        {
            var config = new MqttBrokerProtocolHeadConfig(8883, "localhost");
            var connector = Mock.Of<IMqttBrokerConnector>();

            Mock.Get(connector).Setup(c => c.ConnectAsync(It.IsAny<String>(), It.IsAny<int>())).Returns(Task.CompletedTask);
            Mock.Get(connector).Setup(c => c.DisconnectAsync()).Returns(Task.CompletedTask);

            var sut = new MqttBrokerProtocolHead(config, connector);

            await sut.StartAsync();
            await sut.CloseAsync(CancellationToken.None);
            await sut.CloseAsync(CancellationToken.None);

            Mock.Get(connector).Verify(c => c.DisconnectAsync(), Times.Once);
        }
    }
}
