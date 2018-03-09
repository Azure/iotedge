// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp.Test
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Amqp;
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
    public class EventsLinkHandlerTest
    {
        [Fact]
        public void CreateTest()
        {
            // Arrange
            var connectionHandler = new Mock<IConnectionHandler>();
            var amqpConnection = Mock.Of<IAmqpConnection>(c => c.FindExtension<IConnectionHandler>() == connectionHandler.Object);
            var amqpSession = Mock.Of<IAmqpSession>(s => s.Connection == amqpConnection);
            var amqpLink = Mock.Of<IReceivingAmqpLink>(l => l.Session == amqpSession && l.IsReceiver && l.Settings == new AmqpLinkSettings());

            var requestUri = new Uri("amqps://foo.bar/devices/d1/messages/events");
            var boundVariables = new Dictionary<string, string> { { "deviceid", "d1" } };
            var messageConverter = Mock.Of<IMessageConverter<AmqpMessage>>();

            // Act
            ILinkHandler linkHandler = EventsLinkHandler.Create(amqpLink, requestUri, boundVariables, messageConverter);

            // Assert
            Assert.NotNull(linkHandler);
            Assert.IsType<EventsLinkHandler>(linkHandler);
            Assert.Equal(amqpLink, linkHandler.Link);
            Assert.Equal(requestUri.ToString(), linkHandler.LinkUri.ToString());
        }

        [Fact]
        public void CreateTestForReceiverThrows()
        {
            // Arrange
            var amqpLink = Mock.Of<IAmqpLink>(l => !l.IsReceiver);

            var requestUri = new Uri("amqps://foo.bar/devices/d1/messages/events");
            var boundVariables = new Dictionary<string, string> { { "deviceid", "d1" } };
            var messageConverter = Mock.Of<IMessageConverter<AmqpMessage>>();

            // Act / Assert
            Assert.Throws<InvalidOperationException>(() => EventsLinkHandler.Create(amqpLink, requestUri, boundVariables, messageConverter));
        }

        [Fact]
        public async Task SendMessageTest()
        {
            // Arrange
            var identity = Mock.Of<IIdentity>(i => i.Id == "d1");
            var amqpAuth = new AmqpAuthentication(true, Option.Some(identity));

            IEnumerable<IMessage> receivedMessages = null;
            var deviceListener = Mock.Of<IDeviceListener>();
            Mock.Get(deviceListener).Setup(d => d.ProcessDeviceMessageBatchAsync(It.IsAny<IEnumerable<IMessage>>())).Callback<IEnumerable<IMessage>>(m => receivedMessages = m);

            var connectionHandler = Mock.Of<IConnectionHandler>(c => c.GetAmqpAuthentication() == Task.FromResult(amqpAuth) && c.GetDeviceListener() == Task.FromResult(deviceListener));
            var amqpConnection = Mock.Of<IAmqpConnection>(c => c.FindExtension<IConnectionHandler>() == connectionHandler);
            var amqpSession = Mock.Of<IAmqpSession>(s => s.Connection == amqpConnection);
            var amqpLink = Mock.Of<IReceivingAmqpLink>(l => l.Session == amqpSession && l.IsReceiver && l.Settings == new AmqpLinkSettings() && l.State == AmqpObjectState.Opened);

            Action<AmqpMessage> onMessageCallback = null;
            Mock.Get(amqpLink).Setup(l => l.RegisterMessageListener(It.IsAny<Action<AmqpMessage>>())).Callback<Action<AmqpMessage>>(a => onMessageCallback = a);
            Mock.Get(amqpLink).SetupGet(l => l.Settings).Returns(new AmqpLinkSettings());
            Mock.Get(amqpLink).Setup(l => l.SafeAddClosed(It.IsAny<EventHandler>()));

            var requestUri = new Uri("amqps://foo.bar/devices/d1/messages/events");
            var boundVariables = new Dictionary<string, string> { { "deviceid", "d1" } };
            var messageConverter = new AmqpMessageConverter();

            using (AmqpMessage amqpMessage = AmqpMessage.Create(new MemoryStream(new byte[] { 1, 2, 3, 4 }), false))
            {
                amqpMessage.ApplicationProperties.Map["Prop1"] = "Value1";
                amqpMessage.ApplicationProperties.Map["Prop2"] = "Value2";
                amqpMessage.Properties.ContentType = "application/json";
                amqpMessage.Properties.ContentEncoding = "utf-8";

                ILinkHandler linkHandler = EventsLinkHandler.Create(amqpLink, requestUri, boundVariables, messageConverter);

                // Act
                await linkHandler.OpenAsync(TimeSpan.FromSeconds(30));

                // Assert
                Assert.NotNull(onMessageCallback);

                // Act
                onMessageCallback.Invoke(amqpMessage);

                // Assert
                await WaitAndAssert(
                    () =>
                    {
                        if (receivedMessages == null)
                        {
                            return false;
                        }
                        IList<IMessage> receivedMessagesList = receivedMessages.ToList();
                        Assert.Equal(1, receivedMessagesList.Count);
                        Assert.Equal(receivedMessagesList[0].Properties["Prop1"], "Value1");
                        Assert.Equal(receivedMessagesList[0].Properties["Prop2"], "Value2");
                        Assert.Equal(receivedMessagesList[0].SystemProperties[SystemProperties.ContentEncoding], "utf-8");
                        Assert.Equal(receivedMessagesList[0].SystemProperties[SystemProperties.ContentType], "application/json");
                        Assert.Equal(receivedMessagesList[0].SystemProperties[SystemProperties.ConnectionDeviceId], "d1");
                        Assert.Equal(receivedMessagesList[0].Body, new byte[] { 1, 2, 3, 4 });
                        return true;
                    },
                    TimeSpan.FromSeconds(5));
            }
        }

        [Fact]
        public async Task SendMessageBatchTest()
        {
            // Arrange
            var identity = Mock.Of<IIdentity>(i => i.Id == "d1");
            var amqpAuth = new AmqpAuthentication(true, Option.Some(identity));

            IEnumerable<IMessage> receivedMessages = null;
            var deviceListener = Mock.Of<IDeviceListener>();
            Mock.Get(deviceListener).Setup(d => d.ProcessDeviceMessageBatchAsync(It.IsAny<IEnumerable<IMessage>>()))
                .Callback<IEnumerable<IMessage>>(m => receivedMessages = m)
                .Returns(Task.CompletedTask);

            var connectionHandler = Mock.Of<IConnectionHandler>(c => c.GetAmqpAuthentication() == Task.FromResult(amqpAuth) && c.GetDeviceListener() == Task.FromResult(deviceListener));
            var amqpConnection = Mock.Of<IAmqpConnection>(c => c.FindExtension<IConnectionHandler>() == connectionHandler);
            var amqpSession = Mock.Of<IAmqpSession>(s => s.Connection == amqpConnection);
            var amqpLink = Mock.Of<IReceivingAmqpLink>(l => l.Session == amqpSession && l.IsReceiver && l.Settings == new AmqpLinkSettings() && l.State == AmqpObjectState.Opened);

            Action<AmqpMessage> onMessageCallback = null;
            Mock.Get(amqpLink).Setup(l => l.RegisterMessageListener(It.IsAny<Action<AmqpMessage>>())).Callback<Action<AmqpMessage>>(a => onMessageCallback = a);
            Mock.Get(amqpLink).SetupGet(l => l.Settings).Returns(new AmqpLinkSettings());
            Mock.Get(amqpLink).Setup(l => l.SafeAddClosed(It.IsAny<EventHandler>()));

            var requestUri = new Uri("amqps://foo.bar/devices/d1/messages/events");
            var boundVariables = new Dictionary<string, string>
            {
                { "deviceid", "d1" }
            };
            var messageConverter = new AmqpMessageConverter();

            using (AmqpMessage amqpMessage = AmqpMessage.Create(
                new[]
                {
                    new Data
                    {
                        Value = new ArraySegment<byte>(new byte[80000])
                    },
                    new Data
                    {
                        Value = new ArraySegment<byte>(new byte[90000])
                    },
                    new Data
                    {
                        Value = new ArraySegment<byte>(new byte[100000])
                    }
                }))
            {
                amqpMessage.MessageFormat = AmqpConstants.AmqpBatchedMessageFormat;
                ILinkHandler linkHandler = EventsLinkHandler.Create(amqpLink, requestUri, boundVariables, messageConverter);

                // Act
                await linkHandler.OpenAsync(TimeSpan.FromSeconds(30));

                // Assert
                Assert.NotNull(onMessageCallback);

                // Act
                onMessageCallback.Invoke(amqpMessage);

                // Assert
                await WaitAndAssert(
                    () =>
                    {
                        if (receivedMessages == null)
                        {
                            return false;
                        }
                        IList<IMessage> receivedMessagesList = receivedMessages.ToList();
                        Assert.Equal(3, receivedMessagesList.Count);
                        return true;
                    },
                    TimeSpan.FromSeconds(5));
            }
        }

        [Fact]
        public async Task SendLargeMessageThrowsTest()
        {
            // Arrange
            bool disposeMessageCalled = true;
            var identity = Mock.Of<IIdentity>(i => i.Id == "d1");
            var amqpAuth = new AmqpAuthentication(true, Option.Some(identity));

            var deviceListener = Mock.Of<IDeviceListener>();
            Mock.Get(deviceListener).Setup(d => d.ProcessDeviceMessageBatchAsync(It.IsAny<IEnumerable<IMessage>>()))
                .Returns(Task.CompletedTask);

            var connectionHandler = Mock.Of<IConnectionHandler>(c => c.GetAmqpAuthentication() == Task.FromResult(amqpAuth) && c.GetDeviceListener() == Task.FromResult(deviceListener));
            var amqpConnection = Mock.Of<IAmqpConnection>(c => c.FindExtension<IConnectionHandler>() == connectionHandler);
            var amqpSession = Mock.Of<IAmqpSession>(s => s.Connection == amqpConnection);
            var amqpLink = Mock.Of<IReceivingAmqpLink>(l => l.Session == amqpSession && l.IsReceiver && l.Settings == new AmqpLinkSettings() && l.State == AmqpObjectState.Opened);

            Action<AmqpMessage> onMessageCallback = null;
            Mock.Get(amqpLink).Setup(l => l.RegisterMessageListener(It.IsAny<Action<AmqpMessage>>())).Callback<Action<AmqpMessage>>(a => onMessageCallback = a);
            Mock.Get(amqpLink).SetupGet(l => l.Settings).Returns(new AmqpLinkSettings());
            Mock.Get(amqpLink).Setup(l => l.SafeAddClosed(It.IsAny<EventHandler>()));
            Mock.Get(amqpLink).Setup(l => l.DisposeMessage(It.IsAny<AmqpMessage>(), It.IsAny<Outcome>(), It.IsAny<bool>(), It.IsAny<bool>()))
                .Callback(() => disposeMessageCalled = true);

            var requestUri = new Uri("amqps://foo.bar/devices/d1/messages/events");
            var boundVariables = new Dictionary<string, string> { { "deviceid", "d1" } };
            var messageConverter = new AmqpMessageConverter();

            using (AmqpMessage amqpMessage = AmqpMessage.Create(new MemoryStream(new byte[800000]), false))
            {
                amqpMessage.ApplicationProperties.Map["LargeProp"] = new int[600000];
                ILinkHandler linkHandler = EventsLinkHandler.Create(amqpLink, requestUri, boundVariables, messageConverter);

                // Act
                await linkHandler.OpenAsync(TimeSpan.FromSeconds(30));

                // Assert
                Assert.NotNull(onMessageCallback);

                // Act
                onMessageCallback.Invoke(amqpMessage);

                // Assert
                await WaitAndAssert(() => disposeMessageCalled, TimeSpan.FromSeconds(5));
            }
        }

        static async Task WaitAndAssert(Func<bool> assertBlock, TimeSpan timeout)
        {
            TimeSpan sleepTime = TimeSpan.FromSeconds(1);
            TimeSpan timespan = TimeSpan.Zero;
            while (!assertBlock())
            {
                if (timespan > timeout)
                {
                    Assert.True(false, "Test timed out waiting to complete");
                }
                await Task.Delay(sleepTime);
                timespan += sleepTime;
            }
        }
    }
}
