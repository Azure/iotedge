// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp.Test
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Amqp;
    using Microsoft.Azure.Amqp.Framing;
    using Microsoft.Azure.Devices.Edge.Hub.Amqp.LinkHandlers;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
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
            var identity = Mock.Of<IIdentity>(d => d.Id == "d1");

            // Act
            ILinkHandler linkHandler = new EventsLinkHandler(identity, amqpLink, requestUri, boundVariables, connectionHandler.Object, messageConverter);

            // Assert
            Assert.NotNull(linkHandler);
            Assert.IsType<EventsLinkHandler>(linkHandler);
            Assert.Equal(amqpLink, linkHandler.Link);
            Assert.Equal(requestUri.ToString(), linkHandler.LinkUri.ToString());
        }

        [Fact]
        public async Task SendMessageTest()
        {
            // Arrange
            var identity = Mock.Of<IDeviceIdentity>(i => i.Id == "d1" && i.DeviceId == "d1");

            IEnumerable<IMessage> receivedMessages = null;
            var deviceListener = Mock.Of<IDeviceListener>();
            Mock.Get(deviceListener).Setup(d => d.ProcessDeviceMessageBatchAsync(It.IsAny<IEnumerable<IMessage>>())).Callback<IEnumerable<IMessage>>(m => receivedMessages = m);

            var connectionHandler = Mock.Of<IConnectionHandler>(c => c.GetDeviceListener() == Task.FromResult(deviceListener));
            var amqpAuthenticator = new Mock<IAmqpAuthenticator>();
            amqpAuthenticator.Setup(c => c.AuthenticateAsync("d1")).ReturnsAsync(true);
            Mock<ICbsNode> cbsNodeMock = amqpAuthenticator.As<ICbsNode>();
            ICbsNode cbsNode = cbsNodeMock.Object;
            var amqpConnection = Mock.Of<IAmqpConnection>(
                c =>
                    c.FindExtension<IConnectionHandler>() == connectionHandler &&
                    c.FindExtension<ICbsNode>() == cbsNode);
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

                ILinkHandler linkHandler = new EventsLinkHandler(identity, amqpLink, requestUri, boundVariables, connectionHandler, messageConverter);

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
                        Assert.Equal("Value1", receivedMessagesList[0].Properties["Prop1"]);
                        Assert.Equal("Value2", receivedMessagesList[0].Properties["Prop2"]);
                        Assert.Equal("utf-8", receivedMessagesList[0].SystemProperties[SystemProperties.ContentEncoding]);
                        Assert.Equal("application/json", receivedMessagesList[0].SystemProperties[SystemProperties.ContentType]);
                        Assert.Equal("d1", receivedMessagesList[0].SystemProperties[SystemProperties.ConnectionDeviceId]);
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

            IEnumerable<IMessage> receivedMessages = null;
            var deviceListener = Mock.Of<IDeviceListener>();
            Mock.Get(deviceListener).Setup(d => d.ProcessDeviceMessageBatchAsync(It.IsAny<IEnumerable<IMessage>>()))
                .Callback<IEnumerable<IMessage>>(m => receivedMessages = m)
                .Returns(Task.CompletedTask);

            var connectionHandler = Mock.Of<IConnectionHandler>(c => c.GetDeviceListener() == Task.FromResult(deviceListener));
            var amqpAuthenticator = new Mock<IAmqpAuthenticator>();
            amqpAuthenticator.Setup(c => c.AuthenticateAsync("d1")).ReturnsAsync(true);
            Mock<ICbsNode> cbsNodeMock = amqpAuthenticator.As<ICbsNode>();
            ICbsNode cbsNode = cbsNodeMock.Object;
            var amqpConnection = Mock.Of<IAmqpConnection>(
                c =>
                    c.FindExtension<IConnectionHandler>() == connectionHandler &&
                    c.FindExtension<ICbsNode>() == cbsNode);
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

            string content1 = "Message1 Contents ABC";
            string content2 = "Message2 Contents PQR";
            string content3 = "Message3 Contents XYZ";
            var contents = new List<string>
            {
                content1,
                content2,
                content3
            };
            using (AmqpMessage amqpMessage = GetBatchedMessage(contents))
            {
                amqpMessage.MessageFormat = AmqpConstants.AmqpBatchedMessageFormat;
                ILinkHandler linkHandler = new EventsLinkHandler(identity, amqpLink, requestUri, boundVariables, connectionHandler, messageConverter);

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
                        Assert.Equal(contents.Count, receivedMessagesList.Count);

                        for (int i = 0; i < receivedMessagesList.Count; i++)
                        {
                            IMessage receivedMessage = receivedMessagesList[i];
                            string actualContents = Encoding.UTF8.GetString(receivedMessage.Body);

                            Assert.Equal(contents[i], actualContents);
                            Assert.Equal($"{i}", receivedMessage.SystemProperties[SystemProperties.MessageId]);
                            Assert.Equal($"{i}", receivedMessage.Properties["MsgCnt"]);
                            Assert.Equal(contents[i], receivedMessage.Properties["MsgData"]);
                        }

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

            var deviceListener = Mock.Of<IDeviceListener>();
            Mock.Get(deviceListener).Setup(d => d.ProcessDeviceMessageBatchAsync(It.IsAny<IEnumerable<IMessage>>()))
                .Returns(Task.CompletedTask);

            var connectionHandler = Mock.Of<IConnectionHandler>(c => c.GetDeviceListener() == Task.FromResult(deviceListener));
            var amqpAuthenticator = new Mock<IAmqpAuthenticator>();
            amqpAuthenticator.Setup(c => c.AuthenticateAsync("d1")).ReturnsAsync(true);
            Mock<ICbsNode> cbsNodeMock = amqpAuthenticator.As<ICbsNode>();
            ICbsNode cbsNode = cbsNodeMock.Object;
            var amqpConnection = Mock.Of<IAmqpConnection>(
                c =>
                    c.FindExtension<IConnectionHandler>() == connectionHandler &&
                    c.FindExtension<ICbsNode>() == cbsNode);
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
                ILinkHandler linkHandler = new EventsLinkHandler(identity, amqpLink, requestUri, boundVariables, connectionHandler, messageConverter);

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

        [Fact]
        public void ExpandBatchMessageTest()
        {
            // Arrange
            string content1 = "Message1 Contents ABC";
            string content2 = "Message2 Contents PQR";
            string content3 = "Message3 Contents XYZ";
            var contents = new List<string>
            {
                content1,
                content2,
                content3
            };

            using (AmqpMessage batchedAmqpMessage = GetBatchedMessage(contents))
            {
                // Act
                IList<AmqpMessage> expandedAmqpMessages = EventsLinkHandler.ExpandBatchedMessage(batchedAmqpMessage);

                // Assert
                Assert.NotNull(expandedAmqpMessages);
                Assert.Equal(contents.Count, expandedAmqpMessages.Count);

                for (int i = 0; i < expandedAmqpMessages.Count; i++)
                {
                    AmqpMessage amqpMessage = expandedAmqpMessages[i];
                    string actualContents = Encoding.UTF8.GetString(GetMessageBody(amqpMessage));

                    Assert.Equal(contents[i], actualContents);
                    Assert.Equal($"{i}", amqpMessage.Properties.MessageId);
                    Assert.Equal($"{i}", amqpMessage.ApplicationProperties.Map["MsgCnt"]);
                    Assert.Equal(contents[i], amqpMessage.ApplicationProperties.Map["MsgData"]);
                }
            }
        }

        static byte[] GetMessageBody(AmqpMessage sourceMessage)
        {
            using (var ms = new MemoryStream())
            {
                sourceMessage.BodyStream.CopyTo(ms);
                return ms.ToArray();
            }
        }

        static AmqpMessage GetBatchedMessage(IEnumerable<string> contents)
        {
            var messageList = new List<Data>();
            int ctr = 0;
            foreach (string msgContent in contents)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(msgContent);
                using (AmqpMessage msg = AmqpMessage.Create(new MemoryStream(bytes), false))
                {
                    msg.Properties.MessageId = $"{ctr}";
                    msg.ApplicationProperties = new ApplicationProperties();
                    msg.ApplicationProperties.Map["MsgCnt"] = $"{ctr++}";
                    msg.ApplicationProperties.Map["MsgData"] = msgContent;
                    var data = new Data
                    {
                        Value = ReadStream(msg.ToStream())
                    };
                    messageList.Add(data);
                }
            }

            AmqpMessage amqpMessage = AmqpMessage.Create(messageList);
            amqpMessage.MessageFormat = AmqpConstants.AmqpBatchedMessageFormat;
            return amqpMessage;
        }

        static ArraySegment<byte> ReadStream(Stream stream)
        {
            var memoryStream = new MemoryStream();
            int bytesRead;
            var readBuffer = new byte[512];
            while ((bytesRead = stream.Read(readBuffer, 0, readBuffer.Length)) > 0)
            {
                memoryStream.Write(readBuffer, 0, bytesRead);
            }

            return new ArraySegment<byte>(memoryStream.GetBuffer(), 0, (int)memoryStream.Length);
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
