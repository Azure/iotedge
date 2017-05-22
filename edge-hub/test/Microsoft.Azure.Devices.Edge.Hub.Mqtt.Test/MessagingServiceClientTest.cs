// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt.Test
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Test;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.ProtocolGateway.Messaging;
    using Microsoft.Azure.Devices.Shared;
    using Moq;
    using Xunit;
    using IMessage = Microsoft.Azure.Devices.Edge.Hub.Core.IMessage;
    using IProtocolGatewayMessage = ProtocolGateway.Messaging.IMessage;

    [Unit]
    public class MessagingServiceClientTest
    {
        static readonly Mock<IIdentity> Identity = new Mock<IIdentity>();
        static readonly Mock<IMessagingChannel<IProtocolGatewayMessage>> Channel = new Mock<IMessagingChannel<IProtocolGatewayMessage>>();
        static readonly Mock<IEdgeHub> EdgeHub = new Mock<IEdgeHub>();
        static readonly Mock<IConnectionManager> ConnectionManager = new Mock<IConnectionManager>();
        static readonly IList<string> Input = new List<string>() { "devices/{deviceId}/messages/events/" };
        static readonly IList<string> Output = new List<string>() { "devices/{deviceId}/messages/devicebound", "$iothub/twin/res/{statusCode}/?$rid={correlationId}" };

        struct Messages
        {
            public readonly ProtocolGatewayMessage Source;
            public readonly MqttMessage Expected;

            public Messages(string address, byte[] payload)
            {
                this.Source = new ProtocolGatewayMessage(payload.ToByteBuffer(), address);
                this.Expected = new MqttMessage.Builder(payload).Build();
            }
        }

        static Messages MakeMessages(string address = "dontcare")
        {
            byte[] payload = Encoding.ASCII.GetBytes("abc");
            return new Messages(address, payload);
        }

        static Mock<IDeviceListener> MakeDeviceListenerSpy(byte[] twinBytes)
        {
            var listener = new Mock<IDeviceListener>();
            listener.Setup(x => x.ProcessMessageAsync(It.IsAny<IMessage>()))
                .Returns(Task.CompletedTask);
            listener.Setup(x => x.GetTwinAsync())
                .Returns(Task.FromResult((IMessage)new Message(twinBytes)));
            listener.SetupGet(x => x.Identity)
                .Returns(Mock.Of<IIdentity>());
            return listener;
        }

        static Mock<IDeviceListener> MakeDeviceListenerSpy() => MakeDeviceListenerSpy(new byte[0]);

        static ProtocolGatewayMessageConverter MakeProtocolGatewayMessageConverter()
        {
            var config = new MessageAddressConversionConfiguration(Input, Output);
            var converter = new MessageAddressConverter(config);
            return new ProtocolGatewayMessageConverter(converter);
        }

        [Fact]
        public void ConstructorRequiresADeviceListener()
        {
            var converter = Mock.Of<IMessageConverter<IProtocolGatewayMessage>>();

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
            var message = new ProtocolGatewayMessage(new byte[] { 0 }.ToByteBuffer(), null);
            var listener = Mock.Of<IDeviceListener>();
            var converter = Mock.Of<IMessageConverter<IProtocolGatewayMessage>>();

            var client = new MessagingServiceClient(listener, converter);

            await Assert.ThrowsAsync(typeof(ArgumentException),
                () => client.SendAsync(message));
        }

        [Fact]
        public async Task SendAsyncForwardsMessagesToTheDeviceListener()
        {
            Messages m = MakeMessages();
            Mock<IDeviceListener> listener = MakeDeviceListenerSpy();

            var client = new MessagingServiceClient(listener.Object, MakeProtocolGatewayMessageConverter());
            await client.SendAsync(m.Source);

            listener.Verify(
                x => x.ProcessMessageAsync(It.Is((IMessage actual) => actual.Equals(m.Expected))),
                Times.Once);
        }

        [Fact]
        public async Task SendAsyncRecognizesAGetTwinMessage()
        {
            var message = new ProtocolGatewayMessage(new byte[0].ToByteBuffer(), "$iothub/twin/GET/?$rid=123");
            Mock<IDeviceListener> listener = MakeDeviceListenerSpy();
            var channel = Mock.Of<IMessagingChannel<IProtocolGatewayMessage>>();

            var client = new MessagingServiceClient(listener.Object, MakeProtocolGatewayMessageConverter());
            client.BindMessagingChannel(channel);
            await client.SendAsync(message);

            listener.Verify(x => x.ProcessMessageAsync(It.IsAny<IMessage>()), Times.Never);
            listener.Verify(x => x.GetTwinAsync(), Times.Once);
        }

        [Fact]
        public async Task SendAsyncReturnsTheRequestedTwin()
        {
            byte[] twinBytes = Encoding.UTF8.GetBytes("don't care");
            Mock<IDeviceListener> listener = MakeDeviceListenerSpy(twinBytes);
            var channel = new Mock<IMessagingChannel<IProtocolGatewayMessage>>();
            channel.Setup(x => x.Handle(It.IsAny<IProtocolGatewayMessage>()))
                .Callback<IProtocolGatewayMessage>(
                    msg =>
                    {
                        Assert.Equal(twinBytes, msg.Payload.ToByteArray());
                        Assert.Equal("$iothub/twin/res/200/?$rid=123", msg.Address);
                        Assert.Equal("r", msg.Id);
                    });

            var message = new ProtocolGatewayMessage(new byte[0].ToByteBuffer(), "$iothub/twin/GET/?$rid=123");
            var client = new MessagingServiceClient(listener.Object, MakeProtocolGatewayMessageConverter());
            client.BindMessagingChannel(channel.Object);
            await client.SendAsync(message);

            channel.Verify(x => x.Handle(It.IsAny<IProtocolGatewayMessage>()), Times.Once);
        }

        [Fact]
        public async Task SendAsyncRecognizesAPatchTwinMessage()
        {
            Mock<IDeviceListener> listener = MakeDeviceListenerSpy();
            var channel = Mock.Of<IMessagingChannel<IProtocolGatewayMessage>>();

            var client = new MessagingServiceClient(listener.Object, MakeProtocolGatewayMessageConverter());
            client.BindMessagingChannel(channel);

            string patch = "{\"name\":\"value\"}";
            var message = new ProtocolGatewayMessage(Encoding.UTF8.GetBytes(patch).ToByteBuffer(),
                "$iothub/twin/PATCH/properties/reported/?$rid=123");
            await client.SendAsync(message);

            listener.Verify(x => x.UpdateReportedPropertiesAsync(It.Is<string>((string s) => s.Equals(patch))), Times.Once);
        }

        [Fact]
        public async Task SendAsyncSendsAPatchResponseWhenGivenACorrelationId()
        {
            Mock<IDeviceListener> listener = MakeDeviceListenerSpy();
            var channel = new Mock<IMessagingChannel<IProtocolGatewayMessage>>();
            channel.Setup(x => x.Handle(It.IsAny<IProtocolGatewayMessage>()))
                .Callback<IProtocolGatewayMessage>(
                    msg =>
                    {
                        Assert.Equal("$iothub/twin/res/204/?$rid=123", msg.Address);
                        Assert.Equal("r", msg.Id);
                    });

            var message = new ProtocolGatewayMessage(new byte[0].ToByteBuffer(), "$iothub/twin/PATCH/properties/reported/?$rid=123");
            var client = new MessagingServiceClient(listener.Object, MakeProtocolGatewayMessageConverter());
            client.BindMessagingChannel(channel.Object);
            await client.SendAsync(message);

            channel.Verify(x => x.Handle(It.IsAny<IProtocolGatewayMessage>()), Times.Once);
        }

        [Fact]
        public async Task SendAsyncDoesNotSendAPatchResponseWithoutACorrelationId()
        {
            Mock<IDeviceListener> listener = MakeDeviceListenerSpy();
            var channel = new Mock<IMessagingChannel<IProtocolGatewayMessage>>();
            var message = new ProtocolGatewayMessage(new byte[0].ToByteBuffer(), "$iothub/twin/PATCH/properties/reported/");
            var client = new MessagingServiceClient(listener.Object, MakeProtocolGatewayMessageConverter());
            client.BindMessagingChannel(channel.Object);
            await client.SendAsync(message);

            channel.Verify(x => x.Handle(It.IsAny<IProtocolGatewayMessage>()), Times.Never);
        }

        [Theory]
        [InlineData("$iothub/twin/GET/something")]
        [InlineData("$iothub/twin/PATCH/properties/reported/something")]
        public async Task SendAsyncThrowsIfATwinMessageHasASubresource(string address)
        {
            var message = new ProtocolGatewayMessage(new byte[0].ToByteBuffer(), address);
            var listener = new Mock<IDeviceListener>(MockBehavior.Strict);

            var client = new MessagingServiceClient(listener.Object, MakeProtocolGatewayMessageConverter());

            await Assert.ThrowsAsync(typeof(InvalidOperationException),
                () => client.SendAsync(message));
        }

        [Fact]
        public async Task SendAsyncThrowsIfAGetTwinMessageDoesNotHaveACorrelationId()
        {
            var message = new ProtocolGatewayMessage(new byte[0].ToByteBuffer(), "$iothub/twin/GET/");
            var listener = new Mock<IDeviceListener>(MockBehavior.Strict);

            var client = new MessagingServiceClient(listener.Object, MakeProtocolGatewayMessageConverter());

            await Assert.ThrowsAsync(typeof(InvalidOperationException),
                () => client.SendAsync(message));
        }

        [Fact]
        public async Task SendAsyncThrowsIfTheTwinMessageIsInvalid()
        {
            var message = new ProtocolGatewayMessage(new byte[0].ToByteBuffer(), "$iothub/unknown");
            var listener = new Mock<IDeviceListener>(MockBehavior.Strict);

            var client = new MessagingServiceClient(listener.Object, MakeProtocolGatewayMessageConverter());

            await Assert.ThrowsAsync(typeof(InvalidOperationException),
                () => client.SendAsync(message));
        }

        [Fact]
        public async Task TestReceiveMessagingChannelComplete()
        {
            IProtocolGatewayMessage msg = null;

            ProtocolGatewayMessageConverter messageConverter = MakeProtocolGatewayMessageConverter();
            var dp = new DeviceProxy(Channel.Object, Identity.Object, messageConverter);

            var cloudProxy = new Mock<ICloudProxy>();
            cloudProxy.Setup(d => d.SendFeedbackMessageAsync(It.IsAny<IFeedbackMessage>())).Callback<IFeedbackMessage>(
                m =>
                {
                    Assert.Equal(m.FeedbackStatus, FeedbackStatus.Complete);
                });
            cloudProxy.Setup(d => d.BindCloudListener(It.IsAny<ICloudListener>()));

            var deviceListner = new DeviceListener(Identity.Object, EdgeHub.Object, ConnectionManager.Object, cloudProxy.Object);
            var messagingServiceClient = new Mqtt.MessagingServiceClient(deviceListner, messageConverter);

            Channel.Setup(r => r.Handle(It.IsAny<IProtocolGatewayMessage>()))
                .Callback<IProtocolGatewayMessage>(
                    m =>
                    {
                        msg = m;
                        messagingServiceClient.CompleteAsync(msg.Id);
                    });

            messagingServiceClient.BindMessagingChannel(Channel.Object);
            Core.IMessage message = new MqttMessage.Builder(new byte[] { 1, 2, 3 }).Build();
            await dp.SendMessageAsync(message);

            Assert.NotNull(msg);
        }

        [Fact]
        public async Task TestReceiveMessagingChannelReject()
        {
            IProtocolGatewayMessage msg = null;

            ProtocolGatewayMessageConverter messageConverter = MakeProtocolGatewayMessageConverter();
            var dp = new DeviceProxy(Channel.Object, Identity.Object, messageConverter);
            var cloudProxy = new Mock<ICloudProxy>();
            cloudProxy.Setup(d => d.SendFeedbackMessageAsync(It.IsAny<IFeedbackMessage>())).Callback<IFeedbackMessage>(
                m =>
                {
                    Assert.Equal(m.FeedbackStatus, FeedbackStatus.Reject);
                });
            cloudProxy.Setup(d => d.BindCloudListener(It.IsAny<ICloudListener>()));
            var deviceListner = new DeviceListener(Identity.Object, EdgeHub.Object, ConnectionManager.Object, cloudProxy.Object);
            var messagingServiceClient = new Mqtt.MessagingServiceClient(deviceListner, messageConverter);

            Channel.Setup(r => r.Handle(It.IsAny<IProtocolGatewayMessage>()))
                .Callback<IProtocolGatewayMessage>(
                    m =>
                    {
                        msg = m;
                        messagingServiceClient.RejectAsync(msg.Id);
                    });

            messagingServiceClient.BindMessagingChannel(Channel.Object);
            Core.IMessage message = new MqttMessage.Builder(new byte[] { 1, 2, 3 }).Build();
            await dp.SendMessageAsync(message);

            Assert.NotNull(msg);
        }

        [Fact]
        public async Task TestReceiveMessagingChannelAbandon()
        {
            IProtocolGatewayMessage msg = null;

            ProtocolGatewayMessageConverter messageConverter = MakeProtocolGatewayMessageConverter();
            var dp = new DeviceProxy(Channel.Object, Identity.Object, messageConverter);
            var cloudProxy = new Mock<ICloudProxy>();
            cloudProxy.Setup(d => d.SendFeedbackMessageAsync(It.IsAny<IFeedbackMessage>())).Callback<IFeedbackMessage>(
                m =>
                {
                    Assert.Equal(m.FeedbackStatus, FeedbackStatus.Abandon);
                });
            cloudProxy.Setup(d => d.BindCloudListener(It.IsAny<ICloudListener>()));
            var deviceListner = new DeviceListener(Identity.Object, EdgeHub.Object, ConnectionManager.Object, cloudProxy.Object);
            var messagingServiceClient = new Mqtt.MessagingServiceClient(deviceListner, messageConverter);

            Channel.Setup(r => r.Handle(It.IsAny<IProtocolGatewayMessage>()))
                .Callback<IProtocolGatewayMessage>(
                    m =>
                    {
                        msg = m;
                        messagingServiceClient.AbandonAsync(msg.Id);
                    });

            messagingServiceClient.BindMessagingChannel(Channel.Object);
            Core.IMessage message = new MqttMessage.Builder(new byte[] { 1, 2, 3 }).Build();
            await dp.SendMessageAsync(message);

            Assert.NotNull(msg);
        }

        [Fact]
        public async Task TestReceiveMessagingChannelDispose()
        {
            IProtocolGatewayMessage msg = null;

            ProtocolGatewayMessageConverter messageConverter = MakeProtocolGatewayMessageConverter();
            var dp = new DeviceProxy(Channel.Object, Identity.Object, messageConverter);
            var cloudProxy = new Mock<ICloudProxy>();
            cloudProxy.Setup(d => d.CloseAsync()).Callback(
                () =>
                {

                });
            cloudProxy.Setup(d => d.BindCloudListener(It.IsAny<ICloudListener>()));
            var deviceListner = new DeviceListener(Identity.Object, EdgeHub.Object, ConnectionManager.Object, cloudProxy.Object);
            var messagingServiceClient = new Mqtt.MessagingServiceClient(deviceListner, messageConverter);

            Channel.Setup(r => r.Handle(It.IsAny<IProtocolGatewayMessage>()))
                .Callback<IProtocolGatewayMessage>(
                    m =>
                    {
                        msg = m;
                        messagingServiceClient.DisposeAsync(new Exception("Some issue"));
                    });

            messagingServiceClient.BindMessagingChannel(Channel.Object);
            Core.IMessage message = new MqttMessage.Builder(new byte[] { 1, 2, 3 }).Build();
            await dp.SendMessageAsync(message);

            Assert.NotNull(msg);
        }
    }
}