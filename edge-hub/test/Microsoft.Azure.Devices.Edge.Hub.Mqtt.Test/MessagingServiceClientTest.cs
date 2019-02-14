// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt.Test
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.ProtocolGateway.Messaging;
    using Moq;
    using Xunit;
    using Constants = Microsoft.Azure.Devices.Edge.Hub.Mqtt.Constants;
    using IMessage = Microsoft.Azure.Devices.Edge.Hub.Core.IMessage;
    using IProtocolGatewayMessage = Microsoft.Azure.Devices.ProtocolGateway.Messaging.IMessage;

    [Unit]
    public class MessagingServiceClientTest
    {
        static readonly IByteBufferConverter ByteBufferConverter = new ByteBufferConverter(PooledByteBufferAllocator.Default);
        static readonly IIdentity MockIdentity = Mock.Of<IIdentity>(i => i.Id == "device1");
        static readonly Mock<IMessagingChannel<IProtocolGatewayMessage>> Channel = new Mock<IMessagingChannel<IProtocolGatewayMessage>>();
        static readonly Mock<IEdgeHub> EdgeHub = new Mock<IEdgeHub>();
        static readonly IList<string> Input = new List<string>() { "devices/{deviceId}/messages/events/", "$iothub/methods/res/{statusCode}/?$rid={correlationId}" };

        static readonly IDictionary<string, string> Output = new Dictionary<string, string>
        {
            [Constants.OutboundUriC2D] = "devices/{deviceId}/messages/devicebound",
            [Constants.OutboundUriTwinEndpoint] = "$iothub/twin/res/{statusCode}/?$rid={correlationId}",
            [Constants.OutboundUriModuleEndpoint] = "devices/{deviceId}/module/{moduleId}/endpoint/{endpointId}"
        };

        static readonly Lazy<IMessageConverter<IProtocolGatewayMessage>> ProtocolGatewayMessageConverter = new Lazy<IMessageConverter<IProtocolGatewayMessage>>(MakeProtocolGatewayMessageConverter, true);

        public static IEnumerable<object[]> GenerateInvalidMessageIdData() => new[]
        {
            new object[] { null },
            new object[] { "r" }
        };

        [Fact]
        public void ConstructorRequiresADeviceListener()
        {
            var converter = Mock.Of<IMessageConverter<IProtocolGatewayMessage>>();

            Assert.Throws<ArgumentNullException>(() => new MessagingServiceClient(null, converter, ByteBufferConverter));
        }

        [Fact]
        public void ConstructorRequiresAMessageConverter()
        {
            var listener = Mock.Of<IDeviceListener>();

            Assert.Throws<ArgumentNullException>(() => new MessagingServiceClient(listener, null, ByteBufferConverter));
        }

        [Fact]
        public async Task SendAsyncThrowsIfMessageAddressIsNullOrWhiteSpace()
        {
            ProtocolGatewayMessage message = new ProtocolGatewayMessage.Builder(ByteBufferConverter.ToByteBuffer(new byte[] { 0 }), null)
                .Build();
            var listener = Mock.Of<IDeviceListener>();
            var converter = Mock.Of<IMessageConverter<IProtocolGatewayMessage>>();

            IMessagingServiceClient client = new MessagingServiceClient(listener, converter, ByteBufferConverter);

            await Assert.ThrowsAsync<ArgumentException>(() => client.SendAsync(message));
        }

        [Fact]
        public async Task SendAsyncForwardsMessagesToTheDeviceListener()
        {
            Messages m = MakeMessages("devices/d1/messages/events/");
            Mock<IDeviceListener> listener = MakeDeviceListenerSpy();
            m.Expected.SystemProperties[SystemProperties.ConnectionDeviceId] = "d1";

            var client = new MessagingServiceClient(listener.Object, ProtocolGatewayMessageConverter.Value, ByteBufferConverter);
            await client.SendAsync(m.Source);

            listener.Verify(
                x => x.ProcessDeviceMessageAsync(It.Is((IMessage actual) => actual.Equals(m.Expected))),
                Times.Once);
        }

        [Fact]
        public async Task SendAsyncRecognizesAGetTwinMessage()
        {
            ProtocolGatewayMessage message = new ProtocolGatewayMessage.Builder(ByteBufferConverter.ToByteBuffer(new byte[0]), "$iothub/twin/GET/?$rid=123")
                .Build();
            Mock<IDeviceListener> listener = MakeDeviceListenerSpy();
            var channel = Mock.Of<IMessagingChannel<IProtocolGatewayMessage>>();

            var client = new MessagingServiceClient(listener.Object, ProtocolGatewayMessageConverter.Value, ByteBufferConverter);
            client.BindMessagingChannel(channel);
            await client.SendAsync(message);

            listener.Verify(x => x.ProcessDeviceMessageAsync(It.IsAny<IMessage>()), Times.Never);
            listener.Verify(x => x.SendGetTwinRequest(It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task SendAsyncReturnsTheRequestedTwin()
        {
            byte[] twinBytes = Encoding.UTF8.GetBytes("don't care");
            var edgeHub = Mock.Of<IEdgeHub>(e => e.GetTwinAsync(It.IsAny<string>()) == Task.FromResult(new EdgeMessage.Builder(twinBytes).Build() as IMessage));
            IDeviceListener listener = new DeviceMessageHandler(
                Mock.Of<IIdentity>(i => i.Id == "d1"),
                edgeHub,
                Mock.Of<IConnectionManager>());
            var channel = new Mock<IMessagingChannel<IProtocolGatewayMessage>>();
            channel.Setup(x => x.Handle(It.IsAny<IProtocolGatewayMessage>()))
                .Callback<IProtocolGatewayMessage>(
                    msg =>
                    {
                        Assert.Equal(twinBytes, ByteBufferConverter.ToByteArray(msg.Payload));
                        Assert.Equal("$iothub/twin/res/200/?$rid=123", msg.Address);
                        Assert.Equal("r", msg.Id);
                    });

            ProtocolGatewayMessage message = new ProtocolGatewayMessage.Builder(ByteBufferConverter.ToByteBuffer(new byte[0]), "$iothub/twin/GET/?$rid=123")
                .Build();
            var client = new MessagingServiceClient(listener, ProtocolGatewayMessageConverter.Value, ByteBufferConverter);
            client.BindMessagingChannel(channel.Object);
            await client.SendAsync(message);

            channel.Verify(x => x.Handle(It.IsAny<IProtocolGatewayMessage>()), Times.Once);
        }

        [Fact]
        public async Task SendAsyncRecognizesAPatchTwinMessage()
        {
            Mock<IDeviceListener> listener = MakeDeviceListenerSpy();
            var channel = Mock.Of<IMessagingChannel<IProtocolGatewayMessage>>();

            var client = new MessagingServiceClient(listener.Object, ProtocolGatewayMessageConverter.Value, ByteBufferConverter);
            client.BindMessagingChannel(channel);

            string patch = "{\"name\":\"value\"}";
            ProtocolGatewayMessage message = new ProtocolGatewayMessage.Builder(ByteBufferConverter.ToByteBuffer(Encoding.UTF8.GetBytes(patch)), "$iothub/twin/PATCH/properties/reported/?$rid=123")
                .Build();
            await client.SendAsync(message);

            listener.Verify(x => x.UpdateReportedPropertiesAsync(It.Is((IMessage m) => Encoding.UTF8.GetString(m.Body).Equals(patch)), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task SendAsyncSendsAPatchResponseWhenGivenACorrelationId()
        {
            var edgeHub = Mock.Of<IEdgeHub>(e => e.UpdateReportedPropertiesAsync(It.IsAny<IIdentity>(), It.IsAny<IMessage>()) == Task.CompletedTask);
            IDeviceListener listener = new DeviceMessageHandler(
                Mock.Of<IIdentity>(i => i.Id == "d1"),
                edgeHub,
                Mock.Of<IConnectionManager>());
            var channel = new Mock<IMessagingChannel<IProtocolGatewayMessage>>();
            channel.Setup(x => x.Handle(It.IsAny<IProtocolGatewayMessage>()))
                .Callback<IProtocolGatewayMessage>(
                    msg =>
                    {
                        Assert.Equal("$iothub/twin/res/204/?$rid=123", msg.Address);
                        Assert.Equal("r", msg.Id);
                    });

            ProtocolGatewayMessage message = new ProtocolGatewayMessage.Builder(ByteBufferConverter.ToByteBuffer(new byte[0]), "$iothub/twin/PATCH/properties/reported/?$rid=123")
                .Build();
            var client = new MessagingServiceClient(listener, ProtocolGatewayMessageConverter.Value, ByteBufferConverter);
            client.BindMessagingChannel(channel.Object);
            await client.SendAsync(message);

            channel.Verify(x => x.Handle(It.IsAny<IProtocolGatewayMessage>()), Times.Once);
        }

        [Fact]
        public async Task SendAsyncDoesNotSendAPatchResponseWithoutACorrelationId()
        {
            var edgeHub = Mock.Of<IEdgeHub>(e => e.UpdateReportedPropertiesAsync(It.IsAny<IIdentity>(), It.IsAny<IMessage>()) == Task.CompletedTask);
            IDeviceListener listener = new DeviceMessageHandler(
                Mock.Of<IIdentity>(i => i.Id == "d1"),
                edgeHub,
                Mock.Of<IConnectionManager>());
            var channel = new Mock<IMessagingChannel<IProtocolGatewayMessage>>();
            ProtocolGatewayMessage message = new ProtocolGatewayMessage.Builder(ByteBufferConverter.ToByteBuffer(new byte[0]), "$iothub/twin/PATCH/properties/reported/")
                .Build();
            var client = new MessagingServiceClient(listener, ProtocolGatewayMessageConverter.Value, ByteBufferConverter);
            client.BindMessagingChannel(channel.Object);
            await client.SendAsync(message);

            channel.Verify(x => x.Handle(It.IsAny<IProtocolGatewayMessage>()), Times.Never);
        }

        [Theory]
        [InlineData("$iothub/twin/GET/something")]
        [InlineData("$iothub/twin/PATCH/properties/reported/something")]
        public async Task SendAsyncThrowsIfATwinMessageHasASubresource(string address)
        {
            ProtocolGatewayMessage message = new ProtocolGatewayMessage.Builder(ByteBufferConverter.ToByteBuffer(new byte[0]), address)
                .Build();
            var listener = new Mock<IDeviceListener>(MockBehavior.Strict);

            var client = new MessagingServiceClient(listener.Object, ProtocolGatewayMessageConverter.Value, ByteBufferConverter);

            await Assert.ThrowsAsync<InvalidOperationException>(() => client.SendAsync(message));
        }

        [Fact]
        public async Task SendAsyncThrowsIfAGetTwinMessageDoesNotHaveACorrelationId()
        {
            ProtocolGatewayMessage message = new ProtocolGatewayMessage.Builder(ByteBufferConverter.ToByteBuffer(new byte[0]), "$iothub/twin/GET/")
                .Build();
            var listener = new Mock<IDeviceListener>(MockBehavior.Strict);

            var client = new MessagingServiceClient(listener.Object, ProtocolGatewayMessageConverter.Value, ByteBufferConverter);

            await Assert.ThrowsAsync<InvalidOperationException>(() => client.SendAsync(message));
        }

        [Fact]
        public async Task SendAsyncThrowsIfTheTwinMessageIsInvalid()
        {
            ProtocolGatewayMessage message = new ProtocolGatewayMessage.Builder(ByteBufferConverter.ToByteBuffer(new byte[0]), "$iothub/twin/unknown")
                .Build();
            var listener = new Mock<IDeviceListener>(MockBehavior.Strict);

            var client = new MessagingServiceClient(listener.Object, ProtocolGatewayMessageConverter.Value, ByteBufferConverter);

            await Assert.ThrowsAsync<InvalidOperationException>(() => client.SendAsync(message));
        }

        [Fact]
        public async Task SendAsyncSendsTheRequestedMethod()
        {
            Mock<IDeviceListener> listener = MakeDeviceListenerSpy();

            ProtocolGatewayMessage message = new ProtocolGatewayMessage.Builder(ByteBufferConverter.ToByteBuffer(new byte[0]), "$iothub/methods/res/200/?$rid=123")
                .Build();
            var client = new MessagingServiceClient(listener.Object, ProtocolGatewayMessageConverter.Value, ByteBufferConverter);
            await client.SendAsync(message);

            listener.Verify(p => p.ProcessMethodResponseAsync(It.Is<IMessage>(x => x.Properties[SystemProperties.StatusCode] == "200" && x.Properties[SystemProperties.CorrelationId] == "123")), Times.Once);
        }

        [Theory]
        [Unit]
        [MemberData(nameof(GenerateInvalidMessageIdData))]
        public async Task TestCompleteAsyncDoesNothingWhenMessageIdIsInvalid(string messageId)
        {
            // Arrange
            IMessageConverter<IProtocolGatewayMessage> messageConverter = ProtocolGatewayMessageConverter.Value;
            var deviceListener = new Mock<IDeviceListener>(MockBehavior.Strict);

            // Act
            var messagingServiceClient = new MessagingServiceClient(deviceListener.Object, messageConverter, ByteBufferConverter);
            await messagingServiceClient.CompleteAsync(messageId);

            // Assert
            deviceListener.VerifyAll();
        }

        [Theory]
        [Unit]
        [MemberData(nameof(GenerateInvalidMessageIdData))]
        public async Task TestAbandonAsyncDoesNothingWhenMessageIdIsInvalid(string messageId)
        {
            // Arrange
            IMessageConverter<IProtocolGatewayMessage> messageConverter = ProtocolGatewayMessageConverter.Value;
            var deviceListener = new Mock<IDeviceListener>(MockBehavior.Strict);

            // Act
            var messagingServiceClient = new MessagingServiceClient(deviceListener.Object, messageConverter, ByteBufferConverter);
            await messagingServiceClient.AbandonAsync(messageId);

            // Assert
            deviceListener.VerifyAll();
        }

        [Fact]
        [Unit]
        public async Task TestCompleteAsyncCallsDeviceListener()
        {
            // Arrange
            string messageId = Guid.NewGuid().ToString();
            IMessageConverter<IProtocolGatewayMessage> messageConverter = ProtocolGatewayMessageConverter.Value;
            var deviceListener = new Mock<IDeviceListener>(MockBehavior.Strict);
            deviceListener.Setup(
                    d => d.ProcessMessageFeedbackAsync(
                        It.Is<string>(s => s.Equals(messageId, StringComparison.OrdinalIgnoreCase)),
                        It.Is<FeedbackStatus>(f => f == FeedbackStatus.Complete)))
                .Returns(TaskEx.Done);

            // Act
            var messagingServiceClient = new MessagingServiceClient(deviceListener.Object, messageConverter, ByteBufferConverter);
            await messagingServiceClient.CompleteAsync(messageId);

            // Assert
            deviceListener.VerifyAll();
        }

        [Fact]
        [Unit]
        public async Task TestAbandonAsyncCallsDeviceListener()
        {
            // Arrange
            string messageId = Guid.NewGuid().ToString();
            IMessageConverter<IProtocolGatewayMessage> messageConverter = ProtocolGatewayMessageConverter.Value;
            var deviceListener = new Mock<IDeviceListener>(MockBehavior.Strict);
            deviceListener.Setup(
                    d => d.ProcessMessageFeedbackAsync(
                        It.Is<string>(s => s.Equals(messageId, StringComparison.OrdinalIgnoreCase)),
                        It.Is<FeedbackStatus>(f => f == FeedbackStatus.Abandon)))
                .Returns(TaskEx.Done);

            // Act
            var messagingServiceClient = new MessagingServiceClient(deviceListener.Object, messageConverter, ByteBufferConverter);
            await messagingServiceClient.AbandonAsync(messageId);

            // Assert
            deviceListener.VerifyAll();
        }

        [Fact]
        public async Task TestReceiveMessagingChannelComplete()
        {
            IProtocolGatewayMessage msg = null;

            IMessageConverter<IProtocolGatewayMessage> messageConverter = ProtocolGatewayMessageConverter.Value;
            var dp = new DeviceProxy(Channel.Object, MockIdentity, messageConverter, ByteBufferConverter);

            var cloudProxy = new Mock<ICloudProxy>();
            cloudProxy.Setup(d => d.SendFeedbackMessageAsync(It.IsAny<string>(), It.IsAny<FeedbackStatus>())).Callback<string, FeedbackStatus>(
                (mid, status) => { Assert.Equal(FeedbackStatus.Complete, status); });
            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(c => c.GetCloudConnection(It.IsAny<string>()))
                .Returns(Task.FromResult(Option.Some(cloudProxy.Object)));

            var deviceListner = new DeviceMessageHandler(MockIdentity, EdgeHub.Object, connectionManager.Object);
            var messagingServiceClient = new MessagingServiceClient(deviceListner, messageConverter, ByteBufferConverter);

            Channel.Setup(r => r.Handle(It.IsAny<IProtocolGatewayMessage>()))
                .Callback<IProtocolGatewayMessage>(
                    m =>
                    {
                        msg = m;
                        messagingServiceClient.CompleteAsync(msg.Id);
                    });

            messagingServiceClient.BindMessagingChannel(Channel.Object);
            IMessage message = new EdgeMessage.Builder(new byte[] { 1, 2, 3 }).Build();
            await dp.SendC2DMessageAsync(message);

            Assert.NotNull(msg);
        }

        [Fact]
        public async Task TestReceiveMessagingChannelReject()
        {
            IProtocolGatewayMessage msg = null;

            IMessageConverter<IProtocolGatewayMessage> messageConverter = ProtocolGatewayMessageConverter.Value;
            var dp = new DeviceProxy(Channel.Object, MockIdentity, messageConverter, ByteBufferConverter);
            var cloudProxy = new Mock<ICloudProxy>();
            cloudProxy.Setup(d => d.SendFeedbackMessageAsync(It.IsAny<string>(), It.IsAny<FeedbackStatus>())).Callback<string, FeedbackStatus>(
                (mid, status) => { Assert.Equal(FeedbackStatus.Reject, status); });
            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(c => c.GetCloudConnection(It.IsAny<string>()))
                .Returns(Task.FromResult(Option.Some(cloudProxy.Object)));
            var deviceListner = new DeviceMessageHandler(MockIdentity, EdgeHub.Object, connectionManager.Object);
            var messagingServiceClient = new MessagingServiceClient(deviceListner, messageConverter, ByteBufferConverter);

            Channel.Setup(r => r.Handle(It.IsAny<IProtocolGatewayMessage>()))
                .Callback<IProtocolGatewayMessage>(
                    m =>
                    {
                        msg = m;
                        messagingServiceClient.RejectAsync(msg.Id);
                    });

            messagingServiceClient.BindMessagingChannel(Channel.Object);
            IMessage message = new EdgeMessage.Builder(new byte[] { 1, 2, 3 }).Build();
            await dp.SendC2DMessageAsync(message);

            Assert.NotNull(msg);
        }

        [Fact]
        public async Task TestReceiveMessagingChannelAbandon()
        {
            IProtocolGatewayMessage msg = null;

            IMessageConverter<IProtocolGatewayMessage> messageConverter = ProtocolGatewayMessageConverter.Value;
            var dp = new DeviceProxy(Channel.Object, MockIdentity, messageConverter, ByteBufferConverter);
            var cloudProxy = new Mock<ICloudProxy>();
            cloudProxy.Setup(d => d.SendFeedbackMessageAsync(It.IsAny<string>(), It.IsAny<FeedbackStatus>())).Callback<string, FeedbackStatus>(
                (mid, status) => { Assert.Equal(FeedbackStatus.Abandon, status); });
            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(c => c.GetCloudConnection(It.IsAny<string>()))
                .Returns(Task.FromResult(Option.Some(cloudProxy.Object)));
            var deviceListner = new DeviceMessageHandler(MockIdentity, EdgeHub.Object, connectionManager.Object);
            var messagingServiceClient = new MessagingServiceClient(deviceListner, messageConverter, ByteBufferConverter);

            Channel.Setup(r => r.Handle(It.IsAny<IProtocolGatewayMessage>()))
                .Callback<IProtocolGatewayMessage>(
                    m =>
                    {
                        msg = m;
                        messagingServiceClient.AbandonAsync(msg.Id);
                    });

            messagingServiceClient.BindMessagingChannel(Channel.Object);
            IMessage message = new EdgeMessage.Builder(new byte[] { 1, 2, 3 }).Build();
            await dp.SendC2DMessageAsync(message);

            Assert.NotNull(msg);
        }

        [Fact]
        public async Task TestReceiveMessagingChannelDispose()
        {
            IProtocolGatewayMessage msg = null;

            IMessageConverter<IProtocolGatewayMessage> messageConverter = ProtocolGatewayMessageConverter.Value;
            var dp = new DeviceProxy(Channel.Object, MockIdentity, messageConverter, ByteBufferConverter);
            var cloudProxy = new Mock<ICloudProxy>();
            cloudProxy.Setup(d => d.CloseAsync()).Callback(
                () => { });
            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(c => c.GetCloudConnection(It.IsAny<string>()))
                .Returns(Task.FromResult(Option.Some(cloudProxy.Object)));
            var deviceListner = new DeviceMessageHandler(MockIdentity, EdgeHub.Object, connectionManager.Object);
            var messagingServiceClient = new MessagingServiceClient(deviceListner, messageConverter, ByteBufferConverter);

            Channel.Setup(r => r.Handle(It.IsAny<IProtocolGatewayMessage>()))
                .Callback<IProtocolGatewayMessage>(
                    m =>
                    {
                        msg = m;
                        messagingServiceClient.DisposeAsync(new Exception("Some issue"));
                    });

            messagingServiceClient.BindMessagingChannel(Channel.Object);
            IMessage message = new EdgeMessage.Builder(new byte[] { 1, 2, 3 }).Build();
            await dp.SendC2DMessageAsync(message);

            Assert.NotNull(msg);
        }

        [Fact]
        [Unit]
        public async Task TestMessageCleanup()
        {
            // Arrange
            IMessageConverter<IProtocolGatewayMessage> messageConverter = ProtocolGatewayMessageConverter.Value;
            Mock<IDeviceListener> deviceListener = MakeDeviceListenerSpy();
            var payload = new Mock<IByteBuffer>();
            payload.Setup(p => p.Release()).Returns(true);

            // Act
            var messagingServiceClient = new MessagingServiceClient(deviceListener.Object, messageConverter, ByteBufferConverter);
            IProtocolGatewayMessage protocolGatewayMessage = messagingServiceClient.CreateMessage("devices/Device1/messages/events/", payload.Object);
            await messagingServiceClient.SendAsync(protocolGatewayMessage);

            // Assert
            payload.VerifyAll();
        }

        [Fact]
        [Unit]
        public async Task TestMessageCleanupWhenException()
        {
            // Arrange
            IMessageConverter<IProtocolGatewayMessage> messageConverter = ProtocolGatewayMessageConverter.Value;
            Mock<IDeviceListener> deviceListener = MakeDeviceListenerSpy();
            var payload = new Mock<IByteBuffer>();
            payload.Setup(p => p.Release()).Returns(true);
            Exception expectedException = null;

            // Act
            var messagingServiceClient = new MessagingServiceClient(deviceListener.Object, messageConverter, ByteBufferConverter);
            IProtocolGatewayMessage protocolGatewayMessage = messagingServiceClient.CreateMessage(null, payload.Object);
            try
            {
                await messagingServiceClient.SendAsync(protocolGatewayMessage);
            }
            catch (Exception ex)
            {
                expectedException = ex;
            }

            // Assert
            payload.VerifyAll();
            Assert.NotNull(expectedException);
        }

        static Messages MakeMessages(string address = "dontcare")
        {
            byte[] payload = Encoding.ASCII.GetBytes("abc");
            return new Messages(address, payload);
        }

        static Mock<IDeviceListener> MakeDeviceListenerSpy()
        {
            var listener = new Mock<IDeviceListener>();
            listener.Setup(x => x.ProcessDeviceMessageAsync(It.IsAny<IMessage>()))
                .Returns(Task.CompletedTask);
            listener.Setup(x => x.SendGetTwinRequest(It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            listener.SetupGet(x => x.Identity)
                .Returns(Mock.Of<IIdentity>());
            return listener;
        }

        static ProtocolGatewayMessageConverter MakeProtocolGatewayMessageConverter()
        {
            var config = new MessageAddressConversionConfiguration(Input, Output);
            var converter = new MessageAddressConverter(config);
            return new ProtocolGatewayMessageConverter(converter, ByteBufferConverter);
        }

        struct Messages
        {
            public readonly ProtocolGatewayMessage Source;
            public readonly EdgeMessage Expected;

            public Messages(string address, byte[] payload)
            {
                this.Source = new ProtocolGatewayMessage.Builder(ByteBufferConverter.ToByteBuffer(payload), address)
                    .Build();
                this.Expected = new EdgeMessage.Builder(payload).Build();
            }
        }
    }
}
