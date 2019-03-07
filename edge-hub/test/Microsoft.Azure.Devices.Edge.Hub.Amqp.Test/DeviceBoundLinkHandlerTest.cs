// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp.Test
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Amqp;
    using Microsoft.Azure.Amqp.Encoding;
    using Microsoft.Azure.Amqp.Framing;
    using Microsoft.Azure.Devices.Edge.Hub.Amqp.LinkHandlers;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Unit]
    public class DeviceBoundLinkHandlerTest
    {
        [Fact]
        public void CreateTest()
        {
            // Arrange
            var connectionHandler = Mock.Of<IConnectionHandler>();
            var amqpConnection = Mock.Of<IAmqpConnection>(c => c.FindExtension<IConnectionHandler>() == connectionHandler);
            var amqpSession = Mock.Of<IAmqpSession>(s => s.Connection == amqpConnection);
            var amqpLink = Mock.Of<ISendingAmqpLink>(l => l.Session == amqpSession && !l.IsReceiver);

            var requestUri = new Uri("amqps://foo.bar//devices/d1/messages/deviceBound");
            var boundVariables = new Dictionary<string, string> { { "deviceid", "d1" } };
            var messageConverter = Mock.Of<IMessageConverter<AmqpMessage>>();
            var identity = Mock.Of<IIdentity>(d => d.Id == "d1");

            // Act
            ILinkHandler linkHandler = new DeviceBoundLinkHandler(identity, amqpLink, requestUri, boundVariables, connectionHandler, messageConverter);

            // Assert
            Assert.NotNull(linkHandler);
            Assert.IsType<DeviceBoundLinkHandler>(linkHandler);
            Assert.Equal(amqpLink, linkHandler.Link);
            Assert.Equal(requestUri.ToString(), linkHandler.LinkUri.ToString());
        }

        [Fact]
        public void CreateThrowsExceptionIfReceiverLinkTest()
        {
            // Arrange
            var connectionHandler = Mock.Of<IConnectionHandler>();
            var amqpConnection = Mock.Of<IAmqpConnection>(c => c.FindExtension<IConnectionHandler>() == connectionHandler);
            var amqpSession = Mock.Of<IAmqpSession>(s => s.Connection == amqpConnection);
            var amqpLink = Mock.Of<ISendingAmqpLink>(l => l.Session == amqpSession && l.IsReceiver);

            var requestUri = new Uri("amqps://foo.bar//devices/d1/messages/deviceBound");
            var boundVariables = new Dictionary<string, string> { { "deviceid", "d1" } };
            var messageConverter = Mock.Of<IMessageConverter<AmqpMessage>>();
            var identity = Mock.Of<IIdentity>(d => d.Id == "d1");

            // Act / Assert
            Assert.Throws<ArgumentException>(() => new DeviceBoundLinkHandler(identity, amqpLink, requestUri, boundVariables, connectionHandler, messageConverter));
        }

        [Fact]
        public async Task SendMessageTest()
        {
            // Arrange
            var feedbackStatus = FeedbackStatus.Abandon;
            var deviceListener = new Mock<IDeviceListener>();
            deviceListener.Setup(d => d.ProcessMessageFeedbackAsync(It.IsAny<string>(), It.IsAny<FeedbackStatus>()))
                .Callback<string, FeedbackStatus>((m, s) => feedbackStatus = s)
                .Returns(Task.CompletedTask);
            AmqpMessage receivedAmqpMessage = null;
            var connectionHandler = Mock.Of<IConnectionHandler>(c => c.GetDeviceListener() == Task.FromResult(deviceListener.Object));
            var amqpAuthenticator = new Mock<IAmqpAuthenticator>();
            amqpAuthenticator.Setup(c => c.AuthenticateAsync("d1")).ReturnsAsync(true);
            Mock<ICbsNode> cbsNodeMock = amqpAuthenticator.As<ICbsNode>();
            ICbsNode cbsNode = cbsNodeMock.Object;
            var amqpConnection = Mock.Of<IAmqpConnection>(
                c =>
                    c.FindExtension<IConnectionHandler>() == connectionHandler &&
                    c.FindExtension<ICbsNode>() == cbsNode);
            var amqpSession = Mock.Of<IAmqpSession>(s => s.Connection == amqpConnection);
            var sendingLink = Mock.Of<ISendingAmqpLink>(l => l.Session == amqpSession && !l.IsReceiver && l.Settings == new AmqpLinkSettings() && l.State == AmqpObjectState.Opened);
            Mock.Get(sendingLink).Setup(s => s.SendMessageNoWait(It.IsAny<AmqpMessage>(), It.IsAny<ArraySegment<byte>>(), It.IsAny<ArraySegment<byte>>()))
                .Callback<AmqpMessage, ArraySegment<byte>, ArraySegment<byte>>((m, d, t) => { receivedAmqpMessage = m; });

            var requestUri = new Uri("amqps://foo.bar/devices/d1");
            var boundVariables = new Dictionary<string, string> { { "deviceid", "d1" } };
            var messageConverter = new AmqpMessageConverter();
            var identity = Mock.Of<IIdentity>(d => d.Id == "d1");

            var sendingLinkHandler = new DeviceBoundLinkHandler(identity, sendingLink, requestUri, boundVariables, connectionHandler, messageConverter);
            var body = new byte[] { 0, 1, 2, 3 };
            IMessage message = new EdgeMessage.Builder(body).Build();
            var deliveryState = new Mock<DeliveryState>(new AmqpSymbol(string.Empty), AmqpConstants.AcceptedOutcome.DescriptorCode);
            var delivery = Mock.Of<Delivery>(
                d => d.State == deliveryState.Object
                    && d.DeliveryTag == new ArraySegment<byte>(Guid.NewGuid().ToByteArray()));

            // Act
            await sendingLinkHandler.OpenAsync(TimeSpan.FromSeconds(5));
            await sendingLinkHandler.SendMessage(message);

            // Assert
            Assert.NotNull(receivedAmqpMessage);
            Assert.Equal(body, receivedAmqpMessage.GetPayloadBytes());

            // Act
            sendingLinkHandler.DispositionListener(delivery);
            await Task.Delay(TimeSpan.FromSeconds(5));

            // Assert
            Assert.Equal(FeedbackStatus.Complete, feedbackStatus);
        }
    }
}
