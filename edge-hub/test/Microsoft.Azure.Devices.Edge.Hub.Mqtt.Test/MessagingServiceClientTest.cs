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
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.ProtocolGateway.Messaging;
    using Moq;
    using Xunit;
    using IMessage = Microsoft.Azure.Devices.Edge.Hub.Core.IMessage;
    using IPgMessage = ProtocolGateway.Messaging.IMessage;

    [Unit]
    public class MessagingServiceClientTest
    {
        static readonly Mock<IIdentity> Identity = new Mock<IIdentity>();
        static readonly Mock<IMessagingChannel<IPgMessage>> Channel = new Mock<IMessagingChannel<IPgMessage>>();
        static readonly Mock<IRouter> Router = new Mock<IRouter>();
        static readonly Mock<IDispatcher> Dispacher = new Mock<IDispatcher>();
        static readonly Mock<IConnectionManager> ConnectionManager = new Mock<IConnectionManager>();
        static readonly IList<string> Input = new List<string>() { "devices/{deviceId}/messages/events/" };
        static readonly IList<string> Output = new List<string>() { "devices/{deviceId}/messages/devicebound" };
        struct Messages
        {
            public readonly PgMessage Source;
            public readonly MqttMessage Expected;

            public Messages(string address, byte[] payload)
            {
                this.Source = new PgMessage(payload.ToByteBuffer(), address);
                this.Expected = new MqttMessage.Builder(payload).Build();
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
            listener.Setup(x => x.GetTwin())
                .Returns(Task.FromResult(new Twin()));
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

            var messageAddressConverter = new MessageAddressConverter(new MessageAddressConversionConfiguration(Input, Output));
            var client = new MessagingServiceClient(listener.Object, new PgMessageConverter(messageAddressConverter));
            await client.SendAsync(m.Source);

            listener.Verify(
                x => x.ReceiveMessage(It.Is((IMessage actual) => actual.Equals(m.Expected))),
                Times.Once);
        }

        [Fact]
        public async Task CallsGetTwinOnTheDeviceListener()
        {
            Messages m = MakeMessages("$iothub/whatever");
            Mock<IDeviceListener> listener = MakeDeviceListenerSpy();

            var messageAddressConverter = new MessageAddressConverter(new MessageAddressConversionConfiguration(Input, Output));
            var client = new MessagingServiceClient(listener.Object, new PgMessageConverter(messageAddressConverter));
            await client.SendAsync(m.Source);

            listener.Verify(x => x.ReceiveMessage(It.IsAny<IMessage>()), Times.Never);
            listener.Verify(x => x.GetTwin(), Times.Once);
        }

        [Fact]
        [Unit]
        public async Task TestReceiveMessagingChannelComplete()
        {
            IPgMessage msg = null;

            var messageAddressConverter = new MessageAddressConverter(new MessageAddressConversionConfiguration(Input, Output));
            var messageConverter = new PgMessageConverter(messageAddressConverter);
            var dp = new DeviceProxy(Channel.Object, Identity.Object, messageConverter);

            var cloudProxy = new Mock<ICloudProxy>();
            cloudProxy.Setup(d => d.SendFeedbackMessage(It.IsAny<IFeedbackMessage>())).Callback<IFeedbackMessage>(
                m =>
                {
                    Assert.Equal(m.FeedbackStatus, FeedbackStatus.Complete);
                });
            cloudProxy.Setup(d => d.BindCloudListener(It.IsAny<ICloudListener>()));

            var deviceListner = new DeviceListener(Identity.Object, Router.Object, Dispacher.Object, ConnectionManager.Object, cloudProxy.Object);
            var messagingServiceClient = new Mqtt.MessagingServiceClient(deviceListner, messageConverter);

            Channel.Setup(r => r.Handle(It.IsAny<IPgMessage>()))
                .Callback<IPgMessage>(
                    m =>
                    {
                        msg = m;
                        messagingServiceClient.CompleteAsync(msg.Id);
                    });

            messagingServiceClient.BindMessagingChannel(Channel.Object);
            Core.IMessage message = new MqttMessage.Builder(new byte[] { 1, 2, 3 }).Build();
            await dp.SendMessage(message);

            Assert.NotNull(msg);
        }

        [Fact]
        [Unit]
        public async Task TestReceiveMessagingChannelReject()
        {
            IPgMessage msg = null;

            var messageAddressConverter = new MessageAddressConverter(new MessageAddressConversionConfiguration(Input, Output));
            var messageConverter = new PgMessageConverter(messageAddressConverter);
            var dp = new DeviceProxy(Channel.Object, Identity.Object, messageConverter);
            var cloudProxy = new Mock<ICloudProxy>();
            cloudProxy.Setup(d => d.SendFeedbackMessage(It.IsAny<IFeedbackMessage>())).Callback<IFeedbackMessage>(
                m =>
                {
                    Assert.Equal(m.FeedbackStatus, FeedbackStatus.Reject);
                });
            cloudProxy.Setup(d => d.BindCloudListener(It.IsAny<ICloudListener>()));
            var deviceListner = new DeviceListener(Identity.Object, Router.Object, Dispacher.Object, ConnectionManager.Object, cloudProxy.Object);
            var messagingServiceClient = new Mqtt.MessagingServiceClient(deviceListner, messageConverter);

            Channel.Setup(r => r.Handle(It.IsAny<IPgMessage>()))
                .Callback<IPgMessage>(
                    m =>
                    {
                        msg = m;
                        messagingServiceClient.RejectAsync(msg.Id);
                    });

            messagingServiceClient.BindMessagingChannel(Channel.Object);
            Core.IMessage message = new MqttMessage.Builder(new byte[] { 1, 2, 3 }).Build();
            await dp.SendMessage(message);

            Assert.NotNull(msg);
        }

        [Fact]
        [Unit]
        public async Task TestReceiveMessagingChannelAbandon()
        {
            IPgMessage msg = null;

            var messageAddressConverter = new MessageAddressConverter(new MessageAddressConversionConfiguration(Input, Output));
            var messageConverter = new PgMessageConverter(messageAddressConverter);
            var dp = new DeviceProxy(Channel.Object, Identity.Object, messageConverter);
            var cloudProxy = new Mock<ICloudProxy>();
            cloudProxy.Setup(d => d.SendFeedbackMessage(It.IsAny<IFeedbackMessage>())).Callback<IFeedbackMessage>(
                m =>
                {
                    Assert.Equal(m.FeedbackStatus, FeedbackStatus.Abandon);
                });
            cloudProxy.Setup(d => d.BindCloudListener(It.IsAny<ICloudListener>()));
            var deviceListner = new DeviceListener(Identity.Object, Router.Object, Dispacher.Object, ConnectionManager.Object, cloudProxy.Object);
            var messagingServiceClient = new Mqtt.MessagingServiceClient(deviceListner, messageConverter);

            Channel.Setup(r => r.Handle(It.IsAny<IPgMessage>()))
                .Callback<IPgMessage>(
                    m =>
                    {
                        msg = m;
                        messagingServiceClient.AbandonAsync(msg.Id);
                    });

            messagingServiceClient.BindMessagingChannel(Channel.Object);
            Core.IMessage message = new MqttMessage.Builder(new byte[] { 1, 2, 3 }).Build();
            await dp.SendMessage(message);

            Assert.NotNull(msg);
        }

        [Fact]
        [Unit]
        public async Task TestReceiveMessagingChannelDispose()
        {
            IPgMessage msg = null;

            var messageAddressConverter = new MessageAddressConverter(new MessageAddressConversionConfiguration(Input, Output));
            var messageConverter = new PgMessageConverter(messageAddressConverter);
            var dp = new DeviceProxy(Channel.Object, Identity.Object, messageConverter);
            var cloudProxy = new Mock<ICloudProxy>();
            cloudProxy.Setup(d => d.CloseAsync()).Callback(
                () =>
                {

                });
            cloudProxy.Setup(d => d.BindCloudListener(It.IsAny<ICloudListener>()));
            var deviceListner = new DeviceListener(Identity.Object, Router.Object, Dispacher.Object, ConnectionManager.Object, cloudProxy.Object);
            var messagingServiceClient = new Mqtt.MessagingServiceClient(deviceListner, messageConverter);

            Channel.Setup(r => r.Handle(It.IsAny<IPgMessage>()))
                .Callback<IPgMessage>(
                    m =>
                    {
                        msg = m;
                        messagingServiceClient.DisposeAsync(new Exception("Some issue"));
                    });

            messagingServiceClient.BindMessagingChannel(Channel.Object);
            Core.IMessage message = new MqttMessage.Builder(new byte[] { 1, 2, 3 }).Build();
            await dp.SendMessage(message);

            Assert.NotNull(msg);
        }
    }
}