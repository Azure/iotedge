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
    using Microsoft.Azure.Devices.Edge.Util;
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

            // Act
            ILinkHandler linkHandler = DeviceBoundLinkHandler.Create(amqpLink, requestUri, boundVariables, messageConverter);

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
            var amqpLink = Mock.Of<IAmqpLink>(l => l.IsReceiver);

            var requestUri = new Uri("amqps://foo.bar//devices/d1/messages/deviceBound");
            var boundVariables = new Dictionary<string, string> { { "deviceid", "d1" } };
            var messageConverter = Mock.Of<IMessageConverter<AmqpMessage>>();

            // Act / Assert
            Assert.Throws<InvalidOperationException>(() => DeviceBoundLinkHandler.Create(amqpLink, requestUri, boundVariables, messageConverter));
        }

        [Theory]
        [MemberData(nameof(FeedbackStatusTestData))]
        public void GetFeedbackStatusTest(ulong descriptorCode, FeedbackStatus expectedFeedbackStatus)
        {
            // Arrange
            var deliveryState = new Mock<DeliveryState>(new AmqpSymbol(""), descriptorCode);
            var delivery = Mock.Of<Delivery>(d => d.State == deliveryState.Object);

            // Act
            FeedbackStatus feedbackStatus = DeviceBoundLinkHandler.GetFeedbackStatus(delivery);

            // Assert
            Assert.Equal(feedbackStatus, expectedFeedbackStatus);
        }

        public static IEnumerable<object[]> FeedbackStatusTestData()
        {
            yield return new object[] { AmqpConstants.AcceptedOutcome.DescriptorCode, FeedbackStatus.Complete };
            yield return new object[] { AmqpConstants.RejectedOutcome.DescriptorCode, FeedbackStatus.Reject };
            yield return new object[] { AmqpConstants.ReleasedOutcome.DescriptorCode, FeedbackStatus.Abandon };
        }

        [Fact]
        public async Task SendMessageTest()
        {
            // Arrange
            IDeviceProxy deviceProxy = null;
            var identity = Mock.Of<IIdentity>(i => i.Id == "d1");
            var deviceListener = Mock.Of<IDeviceListener>();
            Mock.Get(deviceListener).Setup(d => d.BindDeviceProxy(It.IsAny<IDeviceProxy>()))
                .Callback<IDeviceProxy>(d => deviceProxy = d);
            Mock.Get(deviceListener).SetupGet(d => d.Identity)
                .Returns(identity);

            var amqpAuthentication = new AmqpAuthentication(true, Option.Some(identity));

            Func<IMessage, Task> callback = null;
            var connectionHandler = new Mock<IConnectionHandler>();
            connectionHandler.Setup(c => c.RegisterC2DMessageSender(It.IsAny<Func<IMessage, Task>>()))
                .Callback<Func<IMessage, Task>>(c => callback = c);
            connectionHandler.Setup(c => c.GetAmqpAuthentication())
                .ReturnsAsync(amqpAuthentication);
            connectionHandler.Setup(c => c.GetDeviceListener())
                .ReturnsAsync(deviceListener);

            var amqpConnection = Mock.Of<IAmqpConnection>(c => c.FindExtension<IConnectionHandler>() == connectionHandler.Object);
            var amqpSession = Mock.Of<IAmqpSession>(s => s.Connection == amqpConnection);
            var amqpLink = Mock.Of<ISendingAmqpLink>(l => l.Session == amqpSession && !l.IsReceiver && l.Settings == new AmqpLinkSettings());
            AmqpMessage receivedAmqpMessage = null;
            Option<ArraySegment<byte>> receivedDeliveryTag = Option.None<ArraySegment<byte>>();
            Option<ArraySegment<byte>> receivedTxnId = Option.None<ArraySegment<byte>>();
            Mock.Get(amqpLink).Setup(m => m.SendMessageNoWait(It.IsAny<AmqpMessage>(), It.IsAny<ArraySegment<byte>>(), It.IsAny<ArraySegment<byte>>()))
                .Callback<AmqpMessage, ArraySegment<byte>, ArraySegment<byte>>(
                    (a, d, t) =>
                    {
                        receivedAmqpMessage = a;
                        receivedDeliveryTag = Option.Some(d);
                        receivedTxnId = Option.Some(t);
                    });

            var requestUri = new Uri("amqps://foo.bar//devices/d1/messages/deviceBound");
            var boundVariables = new Dictionary<string, string> { { "deviceid", "d1" } };
            var messageConverter = new AmqpMessageConverter();

            DateTime expiryTime = DateTime.UtcNow.AddHours(10);
            DateTime enqueueTime = DateTime.UtcNow;
            Guid lockToken = Guid.NewGuid();
            IMessage messageToSend = new EdgeMessage.Builder(new byte[0])
                .SetSystemProperties(new Dictionary<string, string>
                {
                    [SystemProperties.LockToken] = lockToken.ToString(),
                    [SystemProperties.To] = "d1",
                    [SystemProperties.ExpiryTimeUtc] = expiryTime.ToString("o"),
                    [SystemProperties.EnqueuedTime] = enqueueTime.ToString("o")
                })
                .SetProperties(new Dictionary<string, string>
                {
                    ["Prop1"] = "Value1",
                    ["Prop2"] = "Value2"
                })
                .Build();

            // Act
            ILinkHandler linkHandler = DeviceBoundLinkHandler.Create(amqpLink, requestUri, boundVariables, messageConverter);
            await linkHandler.OpenAsync(TimeSpan.FromSeconds(30));
            Assert.NotNull(callback);
            await callback.Invoke(messageToSend);

            // Assert
            Assert.NotNull(receivedAmqpMessage);
            Assert.True(receivedDeliveryTag.HasValue);
            Assert.True(receivedTxnId.HasValue);

            Assert.Equal(new Guid(receivedAmqpMessage.DeliveryTag.Array), lockToken);
            Assert.Equal(receivedAmqpMessage.Properties.To.ToString(), "/devices/d1");
            Assert.Equal(receivedAmqpMessage.Properties.AbsoluteExpiryTime.Value, expiryTime);
            Assert.Equal(receivedAmqpMessage.MessageAnnotations.Map["iothub-enqueuedtime"], enqueueTime);
            Assert.Equal(receivedAmqpMessage.ApplicationProperties.Map["Prop1"], "Value1");
            Assert.Equal(receivedAmqpMessage.ApplicationProperties.Map["Prop2"], "Value2");
            Assert.Equal(new Guid(receivedDeliveryTag.OrDefault().Array), lockToken);
            Assert.Null(receivedTxnId.OrDefault().Array);
        }
    }
}
