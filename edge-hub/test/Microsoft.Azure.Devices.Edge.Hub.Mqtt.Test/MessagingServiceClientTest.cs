// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt.Test
{
    using System;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;
    using IPgMessage = ProtocolGateway.Messaging.IMessage;

    [Unit]
    public class MessagingServiceClientTest
    {
        struct Messages
        {
            public readonly PgMessage Source;
            public readonly MqttMessage Expected;

            public Messages(string address, byte[] payload)
            {
                this.Source = new PgMessage(payload.ToByteBuffer(), address);
                this.Expected = new MqttMessage(payload);
            }
        }

        static Messages MakeMessages(string address = "dontcare")
        {
            byte[] payload = Encoding.ASCII.GetBytes("abc");
            return new Messages(address, payload);
        }

        static Mock<IDeviceListener> MakeDeviceListenerSpy()
        {
            var listener = new Mock<IDeviceListener>();
            listener.Setup(x => x.ReceiveMessage(It.IsAny<IMessage>()))
                .Returns(Task.CompletedTask);
            return listener;
        }

        [Fact]
        public void ConstructorRequiresADeviceListener()
        {
            var converter = Mock.Of<IMessageConverter<IPgMessage>>();

            Assert.Throws(typeof(ArgumentNullException),
                () => new MessagingServiceClient(null, converter));
        }

        [Fact]
        public void ConstructorRequiresAMessageConverter()
        {
            var listener = Mock.Of<IDeviceListener>();

            Assert.Throws(typeof(ArgumentNullException),
                () => new MessagingServiceClient(listener, null));
        }

        [Fact]
        public async Task SendAsyncThrowsIfMessageAddressIsNullOrWhiteSpace()
        {
            var message = new PgMessage(new byte[] { 0 }.ToByteBuffer(), null);
            var listener = Mock.Of<IDeviceListener>();
            var converter = Mock.Of<IMessageConverter<IPgMessage>>();

            var client = new MessagingServiceClient(listener, converter);

            await Assert.ThrowsAsync(typeof(ArgumentException),
                () => client.SendAsync(message));
        }

        [Fact]
        public async Task ForwardsMessagesToTheDeviceListener()
        {
            Messages m = MakeMessages();
            Mock<IDeviceListener> listener = MakeDeviceListenerSpy();

            var client = new MessagingServiceClient(listener.Object, new PgMessageConverter());
            await client.SendAsync(m.Source);

            listener.Verify(
                x => x.ReceiveMessage(It.Is((IMessage actual) => actual.Equals(m.Expected))),
                Times.Once);
        }

        [Fact]
        public async Task DoesNotForwardTwinMessagesToTheDeviceListener()
        {
            Messages m = MakeMessages("$iothub/whatever");
            Mock<IDeviceListener> listener = MakeDeviceListenerSpy();

            var client = new MessagingServiceClient(listener.Object, new PgMessageConverter());
            await client.SendAsync(m.Source);

            listener.Verify(
                x => x.ReceiveMessage(It.Is((IMessage actual) => actual.Equals(m.Expected))),
                Times.Never);
        }
    }
}