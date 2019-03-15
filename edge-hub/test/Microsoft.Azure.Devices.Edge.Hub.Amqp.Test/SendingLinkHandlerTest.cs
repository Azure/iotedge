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
    public class SendingLinkHandlerTest
    {
        public static IEnumerable<object[]> FeedbackStatusTestData()
        {
            yield return new object[] { AmqpConstants.AcceptedOutcome.DescriptorCode, FeedbackStatus.Complete };
            yield return new object[] { AmqpConstants.RejectedOutcome.DescriptorCode, FeedbackStatus.Reject };
            yield return new object[] { AmqpConstants.ReleasedOutcome.DescriptorCode, FeedbackStatus.Abandon };
        }

        [Fact]
        public async Task SendMessageWithFeedbackTest()
        {
            // Arrange
            var feedbackStatus = FeedbackStatus.Abandon;
            var deviceListener = new Mock<IDeviceListener>();
            deviceListener.Setup(d => d.ProcessMessageFeedbackAsync(It.IsAny<string>(), It.IsAny<FeedbackStatus>()))
                .Callback<string, FeedbackStatus>((m, s) => feedbackStatus = s)
                .Returns(Task.CompletedTask);
            AmqpMessage receivedAmqpMessage = null;
            var identity = Mock.Of<IIdentity>(i => i.Id == "d1");
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
            var amqpLinkSettings = new AmqpLinkSettings();
            var sendingLink = Mock.Of<ISendingAmqpLink>(l => l.Session == amqpSession && !l.IsReceiver && l.Settings == amqpLinkSettings && l.State == AmqpObjectState.Opened);
            Mock.Get(sendingLink).Setup(s => s.SendMessageNoWait(It.IsAny<AmqpMessage>(), It.IsAny<ArraySegment<byte>>(), It.IsAny<ArraySegment<byte>>()))
                .Callback<AmqpMessage, ArraySegment<byte>, ArraySegment<byte>>((m, d, t) => { receivedAmqpMessage = m; });

            var requestUri = new Uri("amqps://foo.bar/devices/d1");
            var boundVariables = new Dictionary<string, string> { { "deviceid", "d1" } };
            var messageConverter = new AmqpMessageConverter();

            var sendingLinkHandler = new TestSendingLinkHandler(identity, sendingLink, requestUri, boundVariables, connectionHandler, messageConverter, QualityOfService.AtLeastOnce);
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
            Assert.Null(amqpLinkSettings.SndSettleMode);
            Assert.Equal((byte)ReceiverSettleMode.First, amqpLinkSettings.RcvSettleMode);
        }

        [Fact]
        public async Task SendMessageWithFeedbackExactlyOnceModeTest()
        {
            // Arrange
            var feedbackStatus = FeedbackStatus.Abandon;
            var deviceListener = new Mock<IDeviceListener>();
            deviceListener.Setup(d => d.ProcessMessageFeedbackAsync(It.IsAny<string>(), It.IsAny<FeedbackStatus>()))
                .Callback<string, FeedbackStatus>((m, s) => feedbackStatus = s)
                .Returns(Task.CompletedTask);
            AmqpMessage receivedAmqpMessage = null;
            var identity = Mock.Of<IIdentity>(i => i.Id == "d1");
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
            var amqpLinkSettings = new AmqpLinkSettings();
            var sendingLink = Mock.Of<ISendingAmqpLink>(l => l.Session == amqpSession && !l.IsReceiver && l.Settings == amqpLinkSettings && l.State == AmqpObjectState.Opened);
            Mock.Get(sendingLink).Setup(s => s.SendMessageNoWait(It.IsAny<AmqpMessage>(), It.IsAny<ArraySegment<byte>>(), It.IsAny<ArraySegment<byte>>()))
                .Callback<AmqpMessage, ArraySegment<byte>, ArraySegment<byte>>((m, d, t) => { receivedAmqpMessage = m; });

            var requestUri = new Uri("amqps://foo.bar/devices/d1");
            var boundVariables = new Dictionary<string, string> { { "deviceid", "d1" } };
            var messageConverter = new AmqpMessageConverter();

            var sendingLinkHandler = new TestSendingLinkHandler(identity, sendingLink, requestUri, boundVariables, connectionHandler, messageConverter, QualityOfService.ExactlyOnce);
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
            Assert.Null(amqpLinkSettings.SndSettleMode);
            Assert.Equal((byte)ReceiverSettleMode.Second, amqpLinkSettings.RcvSettleMode);
        }

        [Fact]
        public async Task SendMessageWithNoFeedbackTest()
        {
            // Arrange
            var identity = Mock.Of<IIdentity>(i => i.Id == "d1");
            var deviceListener = new Mock<IDeviceListener>();
            deviceListener.Setup(d => d.ProcessMessageFeedbackAsync(It.IsAny<string>(), It.IsAny<FeedbackStatus>()))
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
            var amqpLinkSettings = new AmqpLinkSettings();
            var sendingLink = Mock.Of<ISendingAmqpLink>(l => l.Session == amqpSession && !l.IsReceiver && l.Settings == amqpLinkSettings && l.State == AmqpObjectState.Opened);
            Mock.Get(sendingLink).Setup(s => s.SendMessageAsync(It.IsAny<AmqpMessage>(), It.IsAny<ArraySegment<byte>>(), It.IsAny<ArraySegment<byte>>(), It.IsAny<TimeSpan>()))
                .Callback<AmqpMessage, ArraySegment<byte>, ArraySegment<byte>, TimeSpan>((m, d, t, ts) => { receivedAmqpMessage = m; })
                .Returns(Task.CompletedTask);

            var requestUri = new Uri("amqps://foo.bar/devices/d1");
            var boundVariables = new Dictionary<string, string> { { "deviceid", "d1" } };
            var messageConverter = new AmqpMessageConverter();

            var sendingLinkHandler = new TestSendingLinkHandler(identity, sendingLink, requestUri, boundVariables, connectionHandler, messageConverter, QualityOfService.AtMostOnce);
            var body = new byte[] { 0, 1, 2, 3 };
            IMessage message = new EdgeMessage.Builder(body).Build();

            // Act
            await sendingLinkHandler.OpenAsync(TimeSpan.FromSeconds(5));
            await sendingLinkHandler.SendMessage(message);

            // Assert
            Assert.NotNull(receivedAmqpMessage);
            Assert.Equal(body, receivedAmqpMessage.GetPayloadBytes());
            Assert.Equal((byte)SenderSettleMode.Settled, amqpLinkSettings.SndSettleMode);
            Assert.Equal((byte)ReceiverSettleMode.First, amqpLinkSettings.RcvSettleMode);
        }

        [Theory]
        [MemberData(nameof(FeedbackStatusTestData))]
        public void GetFeedbackStatusTest(ulong descriptorCode, FeedbackStatus expectedFeedbackStatus)
        {
            // Arrange
            var deliveryState = new Mock<DeliveryState>(new AmqpSymbol(string.Empty), descriptorCode);
            var delivery = Mock.Of<Delivery>(d => d.State == deliveryState.Object);

            // Act
            FeedbackStatus feedbackStatus = SendingLinkHandler.GetFeedbackStatus(delivery);

            // Assert
            Assert.Equal(feedbackStatus, expectedFeedbackStatus);
        }
    }

    class TestSendingLinkHandler : SendingLinkHandler
    {
        public TestSendingLinkHandler(
            IIdentity identity,
            ISendingAmqpLink link,
            Uri requestUri,
            IDictionary<string, string> boundVariables,
            IConnectionHandler connectionHandler,
            IMessageConverter<AmqpMessage> messageConverter,
            QualityOfService qualityOfService)
            : base(identity, link, requestUri, boundVariables, connectionHandler, messageConverter)
        {
            this.QualityOfService = qualityOfService;
        }

        public override LinkType Type => LinkType.ModuleMessages; // Some value

        protected override QualityOfService QualityOfService { get; }
    }
}
