// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using Microsoft.Azure.Devices.Edge.Hub.Amqp.LinkHandlers;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;

    using Moq;

    using Xunit;

    using Constants = Microsoft.Azure.Devices.Edge.Hub.Amqp.Constants;

    [Unit]
    public class ConnectionHandlerTest
    {
        [Fact]
        public async Task CloseOnRemovingAllLinksTest()
        {
            // Arrange
            var deviceListener = new Mock<IDeviceListener>();
            deviceListener.Setup(d => d.CloseAsync()).Returns(Task.CompletedTask);
            var identity = Mock.Of<IIdentity>(i => i.Id == "d1/m1");
            var clientCredentials = Mock.Of<IClientCredentials>(c => c.Identity == identity);
            var connectionProvider = Mock.Of<IConnectionProvider>(c => c.GetDeviceListenerAsync(clientCredentials) == Task.FromResult(deviceListener.Object));
            deviceListener.Setup(d => d.BindDeviceProxy(It.IsAny<IDeviceProxy>()));

            var amqpAuthentication = new AmqpAuthentication(true, Option.Some(clientCredentials));
            var cbsNode = Mock.Of<ICbsNode>(c => c.GetAmqpAuthentication() == Task.FromResult(amqpAuthentication));
            var amqpConnection = Mock.Of<IAmqpConnection>(c => c.FindExtension<ICbsNode>() == cbsNode);
            var connectionHandler = new ConnectionHandler(amqpConnection, connectionProvider);

            var eventsLinkHandler = Mock.Of<ILinkHandler>(l => l.Type == LinkType.Events);
            string twinCorrelationId = Guid.NewGuid().ToString();
            var twinReceivingLinkHander = Mock.Of<ILinkHandler>(l => l.Type == LinkType.TwinReceiving && l.CorrelationId == twinCorrelationId);
            var twinSendingLinkHandler = Mock.Of<ILinkHandler>(l => l.Type == LinkType.TwinSending && l.CorrelationId == twinCorrelationId);
            string methodCorrelationId = Guid.NewGuid().ToString();
            var methodReceivingLinkHander = Mock.Of<ILinkHandler>(l => l.Type == LinkType.MethodReceiving && l.CorrelationId == methodCorrelationId);
            var methodSendingLinkHandler = Mock.Of<ILinkHandler>(l => l.Type == LinkType.MethodSending && l.CorrelationId == methodCorrelationId);

            // Act
            await connectionHandler.GetDeviceListener();
            await connectionHandler.RegisterLinkHandler(eventsLinkHandler);
            await connectionHandler.RegisterLinkHandler(twinReceivingLinkHander);
            await connectionHandler.RegisterLinkHandler(twinSendingLinkHandler);
            await connectionHandler.RegisterLinkHandler(methodSendingLinkHandler);
            await connectionHandler.RegisterLinkHandler(methodReceivingLinkHander);

            await connectionHandler.RemoveLinkHandler(eventsLinkHandler);
            await connectionHandler.RemoveLinkHandler(twinReceivingLinkHander);
            await connectionHandler.RemoveLinkHandler(twinSendingLinkHandler);
            await connectionHandler.RemoveLinkHandler(methodSendingLinkHandler);

            // Assert
            deviceListener.Verify(d => d.CloseAsync(), Times.Never);

            // Act
            await connectionHandler.RemoveLinkHandler(methodReceivingLinkHander);

            // Assert
            deviceListener.Verify(d => d.CloseAsync(), Times.Once);
        }

        [Fact]
        public void ConnectionHandlerCtorTest()
        {
            // Arrange
            var amqpConnection = Mock.Of<IAmqpConnection>();
            var connectionPovider = Mock.Of<IConnectionProvider>();

            // Act / Assert
            Assert.NotNull(new ConnectionHandler(amqpConnection, connectionPovider));
            Assert.Throws<ArgumentNullException>(() => new ConnectionHandler(null, connectionPovider));
            Assert.Throws<ArgumentNullException>(() => new ConnectionHandler(amqpConnection, null));
        }

        [Fact]
        public async Task GetAmqpAuthenticationTest()
        {
            // Arrange
            var identity = Mock.Of<IIdentity>(i => i.Id == "d1/m1");
            var clientCredentials = Mock.Of<IClientCredentials>(c => c.Identity == identity);
            var deviceListener = Mock.Of<IDeviceListener>();
            Mock.Get(deviceListener).Setup(d => d.BindDeviceProxy(It.IsAny<IDeviceProxy>()));

            var connectionProvider = Mock.Of<IConnectionProvider>(c => c.GetDeviceListenerAsync(clientCredentials) == Task.FromResult(deviceListener));

            var amqpAuthentication = new AmqpAuthentication(true, Option.Some(clientCredentials));
            var cbsNode = Mock.Of<ICbsNode>(c => c.GetAmqpAuthentication() == Task.FromResult(amqpAuthentication));
            var amqpConnection = Mock.Of<IAmqpConnection>(c => c.FindExtension<ICbsNode>() == cbsNode);
            var connectionHandler = new ConnectionHandler(amqpConnection, connectionProvider);

            // Act
            var tasks = new List<Task<AmqpAuthentication>>();
            for (int i = 0; i < 10; i++)
            {
                tasks.Add(connectionHandler.GetAmqpAuthentication());
            }

            IList<AmqpAuthentication> amqpAuthentications = (await Task.WhenAll(tasks)).ToList();

            // Assert
            Assert.NotNull(amqpAuthentications);
            Assert.Equal(10, amqpAuthentications.Count);
            for (int i = 0; i < 10; i++)
            {
                Assert.Equal(amqpAuthentication, amqpAuthentications[0]);
            }

            Assert.True(amqpAuthentications[0].IsAuthenticated);
            Assert.Equal(identity, amqpAuthentications[0].ClientCredentials.OrDefault().Identity);
            Mock.Get(connectionProvider).Verify(c => c.GetDeviceListenerAsync(It.IsAny<IClientCredentials>()), Times.AtMostOnce);
            Mock.Get(cbsNode).Verify(d => d.GetAmqpAuthentication(), Times.AtMostOnce);
        }

        [Fact]
        public async Task GetDeviceListenerTest()
        {
            // Arrange
            IDeviceProxy deviceProxy = null;
            var identity = Mock.Of<IIdentity>(i => i.Id == "d1/m1");
            var clientCredentials = Mock.Of<IClientCredentials>(c => c.Identity == identity);
            var deviceListener = Mock.Of<IDeviceListener>();
            Mock.Get(deviceListener).Setup(d => d.BindDeviceProxy(It.IsAny<IDeviceProxy>()))
                .Callback<IDeviceProxy>(d => deviceProxy = d);

            var connectionProvider = Mock.Of<IConnectionProvider>(c => c.GetDeviceListenerAsync(clientCredentials) == Task.FromResult(deviceListener));

            var amqpAuthentication = new AmqpAuthentication(true, Option.Some(clientCredentials));
            var cbsNode = Mock.Of<ICbsNode>(c => c.GetAmqpAuthentication() == Task.FromResult(amqpAuthentication));
            var amqpConnection = Mock.Of<IAmqpConnection>(c => c.FindExtension<ICbsNode>() == cbsNode);
            var connectionHandler = new ConnectionHandler(amqpConnection, connectionProvider);

            // Act
            var tasks = new List<Task<IDeviceListener>>();
            for (int i = 0; i < 10; i++)
            {
                tasks.Add(connectionHandler.GetDeviceListener());
            }

            IList<IDeviceListener> deviceListeners = (await Task.WhenAll(tasks)).ToList();

            // Assert
            Assert.NotNull(deviceListeners);
            Assert.Equal(10, deviceListeners.Count);
            for (int i = 0; i < 10; i++)
            {
                Assert.Equal(deviceListener, deviceListeners[0]);
            }

            Assert.NotNull(deviceProxy);
            Mock.Get(connectionProvider).Verify(c => c.GetDeviceListenerAsync(It.IsAny<IClientCredentials>()), Times.AtMostOnce);
            Mock.Get(deviceListener).Verify(d => d.BindDeviceProxy(It.IsAny<IDeviceProxy>()), Times.AtMostOnce);
        }

        [Fact]
        public async Task RegisterC2DMessageSenderTest()
        {
            // Arrange
            IDeviceProxy deviceProxy = null;
            var identity = Mock.Of<IIdentity>(i => i.Id == "d1");
            var clientCredentials = Mock.Of<IClientCredentials>(c => c.Identity == identity);
            var deviceListener = Mock.Of<IDeviceListener>();
            Mock.Get(deviceListener).Setup(d => d.BindDeviceProxy(It.IsAny<IDeviceProxy>()))
                .Callback<IDeviceProxy>(d => deviceProxy = d);

            var connectionProvider = Mock.Of<IConnectionProvider>(c => c.GetDeviceListenerAsync(clientCredentials) == Task.FromResult(deviceListener));

            var amqpAuthentication = new AmqpAuthentication(true, Option.Some(clientCredentials));
            var cbsNode = Mock.Of<ICbsNode>(c => c.GetAmqpAuthentication() == Task.FromResult(amqpAuthentication));
            var amqpConnection = Mock.Of<IAmqpConnection>(c => c.FindExtension<ICbsNode>() == cbsNode);
            var connectionHandler = new ConnectionHandler(amqpConnection, connectionProvider);

            IMessage receivedMessage = null;
            var c2DLinkHandler = new Mock<ISendingLinkHandler>();
            c2DLinkHandler.Setup(c => c.SendMessage(It.IsAny<IMessage>()))
                .Callback<IMessage>(m => receivedMessage = m)
                .Returns(Task.CompletedTask);
            c2DLinkHandler.SetupGet(c => c.Type)
                .Returns(LinkType.C2D);

            var systemProperties = new Dictionary<string, string>();
            var messageToSend = Mock.Of<IMessage>(m => m.SystemProperties == systemProperties);

            // Act
            await connectionHandler.GetDeviceListener();
            await connectionHandler.RegisterLinkHandler(c2DLinkHandler.Object);
            await deviceProxy.SendC2DMessageAsync(messageToSend);

            // Assert
            Assert.NotNull(receivedMessage);
            Assert.Equal(messageToSend, receivedMessage);
            Assert.Equal(systemProperties[SystemProperties.To], "/devices/d1");
        }

        [Fact]
        public async Task RegisterDesiredPropertiesUpdateSenderTest()
        {
            // Arrange
            IDeviceProxy deviceProxy = null;
            var identity = Mock.Of<IIdentity>(i => i.Id == "d1/m1");
            var clientCredentials = Mock.Of<IClientCredentials>(c => c.Identity == identity);
            var deviceListener = Mock.Of<IDeviceListener>();
            Mock.Get(deviceListener).Setup(d => d.BindDeviceProxy(It.IsAny<IDeviceProxy>()))
                .Callback<IDeviceProxy>(d => deviceProxy = d);

            var connectionProvider = Mock.Of<IConnectionProvider>(c => c.GetDeviceListenerAsync(clientCredentials) == Task.FromResult(deviceListener));

            var amqpAuthentication = new AmqpAuthentication(true, Option.Some(clientCredentials));
            var cbsNode = Mock.Of<ICbsNode>(c => c.GetAmqpAuthentication() == Task.FromResult(amqpAuthentication));
            var amqpConnection = Mock.Of<IAmqpConnection>(c => c.FindExtension<ICbsNode>() == cbsNode);
            var connectionHandler = new ConnectionHandler(amqpConnection, connectionProvider);

            IMessage receivedMessage = null;
            var twinSendingLinkHandler = new Mock<ISendingLinkHandler>();
            twinSendingLinkHandler.Setup(c => c.SendMessage(It.IsAny<IMessage>()))
                .Callback<IMessage>(m => receivedMessage = m)
                .Returns(Task.CompletedTask);
            twinSendingLinkHandler.SetupGet(c => c.Type)
                .Returns(LinkType.TwinSending);

            var messageToSend = Mock.Of<IMessage>();

            // Act
            await connectionHandler.GetDeviceListener();
            await connectionHandler.RegisterLinkHandler(twinSendingLinkHandler.Object);
            await deviceProxy.OnDesiredPropertyUpdates(messageToSend);

            // Assert
            Assert.NotNull(receivedMessage);
            Assert.Equal(messageToSend, receivedMessage);
        }

        [Fact]
        public async Task RegisterMethodInvokerTest()
        {
            // Arrange
            IDeviceProxy deviceProxy = null;
            var identity = Mock.Of<IIdentity>(i => i.Id == "d1/m1");
            var clientCredentials = Mock.Of<IClientCredentials>(c => c.Identity == identity);
            var deviceListener = Mock.Of<IDeviceListener>();
            Mock.Get(deviceListener).Setup(d => d.BindDeviceProxy(It.IsAny<IDeviceProxy>()))
                .Callback<IDeviceProxy>(d => deviceProxy = d);

            var connectionProvider = Mock.Of<IConnectionProvider>(c => c.GetDeviceListenerAsync(clientCredentials) == Task.FromResult(deviceListener));

            var amqpAuthentication = new AmqpAuthentication(true, Option.Some(clientCredentials));
            var cbsNode = Mock.Of<ICbsNode>(c => c.GetAmqpAuthentication() == Task.FromResult(amqpAuthentication));
            var amqpConnection = Mock.Of<IAmqpConnection>(c => c.FindExtension<ICbsNode>() == cbsNode);
            var connectionHandler = new ConnectionHandler(amqpConnection, connectionProvider);

            IMessage receivedMessage = null;
            var methodSendingLinkHandler = new Mock<ISendingLinkHandler>();
            methodSendingLinkHandler.Setup(c => c.SendMessage(It.IsAny<IMessage>()))
                .Callback<IMessage>(m => receivedMessage = m)
                .Returns(Task.CompletedTask);
            methodSendingLinkHandler.SetupGet(c => c.Type)
                .Returns(LinkType.MethodSending);

            var sentRequest = new DirectMethodRequest(identity.Id, "poke", new byte[] { 0, 1, 2 }, TimeSpan.FromSeconds(10));

            // Act
            await connectionHandler.GetDeviceListener();
            await connectionHandler.RegisterLinkHandler(methodSendingLinkHandler.Object);
            await deviceProxy.InvokeMethodAsync(sentRequest);

            // Assert
            Assert.NotNull(receivedMessage);
            Assert.Equal(sentRequest.Data, receivedMessage.Body);
            Assert.Equal(sentRequest.CorrelationId, receivedMessage.SystemProperties[SystemProperties.CorrelationId]);
            Assert.Equal(sentRequest.Name, receivedMessage.Properties[Constants.MessagePropertiesMethodNameKey]);
        }

        [Fact]
        public async Task RegisterModuleMessageSenderTest()
        {
            // Arrange
            IDeviceProxy deviceProxy = null;
            var identity = Mock.Of<IIdentity>(i => i.Id == "d1/m1");
            var clientCredentials = Mock.Of<IClientCredentials>(c => c.Identity == identity);
            var deviceListener = Mock.Of<IDeviceListener>();
            Mock.Get(deviceListener).Setup(d => d.BindDeviceProxy(It.IsAny<IDeviceProxy>()))
                .Callback<IDeviceProxy>(d => deviceProxy = d);

            var connectionProvider = Mock.Of<IConnectionProvider>(c => c.GetDeviceListenerAsync(clientCredentials) == Task.FromResult(deviceListener));

            var amqpAuthentication = new AmqpAuthentication(true, Option.Some(clientCredentials));
            var cbsNode = Mock.Of<ICbsNode>(c => c.GetAmqpAuthentication() == Task.FromResult(amqpAuthentication));
            var amqpConnection = Mock.Of<IAmqpConnection>(c => c.FindExtension<ICbsNode>() == cbsNode);
            var connectionHandler = new ConnectionHandler(amqpConnection, connectionProvider);

            IMessage receivedMessage = null;
            var moduleMessageLinkHandler = new Mock<ISendingLinkHandler>();
            moduleMessageLinkHandler.Setup(c => c.SendMessage(It.IsAny<IMessage>()))
                .Callback<IMessage>(m => receivedMessage = m)
                .Returns(Task.CompletedTask);
            moduleMessageLinkHandler.SetupGet(c => c.Type)
                .Returns(LinkType.ModuleMessages);

            var systemProperties = new Dictionary<string, string>();
            var messageToSend = Mock.Of<IMessage>(m => m.SystemProperties == systemProperties);

            // Act
            await connectionHandler.GetDeviceListener();
            await connectionHandler.RegisterLinkHandler(moduleMessageLinkHandler.Object);
            await deviceProxy.SendMessageAsync(messageToSend, "i1");

            // Assert
            Assert.NotNull(receivedMessage);
            Assert.Equal(messageToSend, receivedMessage);
            Assert.Equal(systemProperties[SystemProperties.InputName], "i1");
        }
    }
}
