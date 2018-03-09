// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Unit]
    public class ConnectionHandlerTest
    {
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
        public async Task GetDeviceListenerTest()
        {
            // Arrange
            IDeviceProxy deviceProxy = null;
            var identity = Mock.Of<IIdentity>(i => i.Id == "d1/m1");
            var deviceListener = Mock.Of<IDeviceListener>();
            Mock.Get(deviceListener).Setup(d => d.BindDeviceProxy(It.IsAny<IDeviceProxy>()))
                .Callback<IDeviceProxy>(d => deviceProxy = d);

            var connectionProvider = Mock.Of<IConnectionProvider>(c => c.GetDeviceListenerAsync(identity) == Task.FromResult(deviceListener));

            var amqpAuthentication = new AmqpAuthentication(true, Option.Some(identity));
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
            Mock.Get(connectionProvider).Verify(c => c.GetDeviceListenerAsync(It.IsAny<IIdentity>()), Times.AtMostOnce);
            Mock.Get(deviceListener).Verify(d => d.BindDeviceProxy(It.IsAny<IDeviceProxy>()), Times.AtMostOnce);
        }

        [Fact]
        public async Task GetAmqpAuthenticationTest()
        {
            // Arrange
            var identity = Mock.Of<IIdentity>(i => i.Id == "d1/m1");
            var deviceListener = Mock.Of<IDeviceListener>();
            Mock.Get(deviceListener).Setup(d => d.BindDeviceProxy(It.IsAny<IDeviceProxy>()));

            var connectionProvider = Mock.Of<IConnectionProvider>(c => c.GetDeviceListenerAsync(identity) == Task.FromResult(deviceListener));

            var amqpAuthentication = new AmqpAuthentication(true, Option.Some(identity));
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
            Assert.Equal(identity, amqpAuthentications[0].Identity.OrDefault());
            Mock.Get(connectionProvider).Verify(c => c.GetDeviceListenerAsync(It.IsAny<IIdentity>()), Times.AtMostOnce);
            Mock.Get(cbsNode).Verify(d => d.GetAmqpAuthentication(), Times.AtMostOnce);
        }

        [Fact]
        public async Task RegisterC2DMessageSenderTest()
        {
            // Arrange
            IDeviceProxy deviceProxy = null;
            var identity = Mock.Of<IIdentity>(i => i.Id == "d1/m1");
            var deviceListener = Mock.Of<IDeviceListener>();
            Mock.Get(deviceListener).Setup(d => d.BindDeviceProxy(It.IsAny<IDeviceProxy>()))
                .Callback<IDeviceProxy>(d => deviceProxy = d);

            var connectionProvider = Mock.Of<IConnectionProvider>(c => c.GetDeviceListenerAsync(identity) == Task.FromResult(deviceListener));

            var amqpAuthentication = new AmqpAuthentication(true, Option.Some(identity));
            var cbsNode = Mock.Of<ICbsNode>(c => c.GetAmqpAuthentication() == Task.FromResult(amqpAuthentication));
            var amqpConnection = Mock.Of<IAmqpConnection>(c => c.FindExtension<ICbsNode>() == cbsNode);
            var connectionHandler = new ConnectionHandler(amqpConnection, connectionProvider);

            IMessage receivedMessage = null;
            Task Handler(IMessage message)
            {
                receivedMessage = message;
                return Task.CompletedTask;
            }

            var messageToSend = Mock.Of<IMessage>();

            // Act
            await connectionHandler.GetDeviceListener();
            connectionHandler.RegisterC2DMessageSender(Handler);
            await deviceProxy.SendC2DMessageAsync(messageToSend);

            // Assert
            Assert.NotNull(receivedMessage);
            Assert.Equal(messageToSend, receivedMessage);
        }

        [Fact]
        public async Task RegisterModuleMessageSenderTest()
        {
            // Arrange
            IDeviceProxy deviceProxy = null;
            var identity = Mock.Of<IIdentity>(i => i.Id == "d1/m1");
            var deviceListener = Mock.Of<IDeviceListener>();
            Mock.Get(deviceListener).Setup(d => d.BindDeviceProxy(It.IsAny<IDeviceProxy>()))
                .Callback<IDeviceProxy>(d => deviceProxy = d);

            var connectionProvider = Mock.Of<IConnectionProvider>(c => c.GetDeviceListenerAsync(identity) == Task.FromResult(deviceListener));

            var amqpAuthentication = new AmqpAuthentication(true, Option.Some(identity));
            var cbsNode = Mock.Of<ICbsNode>(c => c.GetAmqpAuthentication() == Task.FromResult(amqpAuthentication));
            var amqpConnection = Mock.Of<IAmqpConnection>(c => c.FindExtension<ICbsNode>() == cbsNode);
            var connectionHandler = new ConnectionHandler(amqpConnection, connectionProvider);

            string receivedInput = null;
            IMessage receivedMessage = null;
            Task Handler(string input, IMessage message)
            {
                receivedMessage = message;
                receivedInput = input;
                return Task.CompletedTask;
            }

            var messageToSend = Mock.Of<IMessage>();

            // Act
            await connectionHandler.GetDeviceListener();
            connectionHandler.RegisterModuleMessageSender(Handler);
            await deviceProxy.SendMessageAsync(messageToSend, "i1");

            // Assert
            Assert.NotNull(receivedMessage);
            Assert.Equal(messageToSend, receivedMessage);
            Assert.Equal(receivedInput, "i1");
        }

        [Fact]
        public async Task RegisterMethodInvokerTest()
        {
            // Arrange
            IDeviceProxy deviceProxy = null;
            var identity = Mock.Of<IIdentity>(i => i.Id == "d1/m1");
            var deviceListener = Mock.Of<IDeviceListener>();
            Mock.Get(deviceListener).Setup(d => d.BindDeviceProxy(It.IsAny<IDeviceProxy>()))
                .Callback<IDeviceProxy>(d => deviceProxy = d);

            var connectionProvider = Mock.Of<IConnectionProvider>(c => c.GetDeviceListenerAsync(identity) == Task.FromResult(deviceListener));

            var amqpAuthentication = new AmqpAuthentication(true, Option.Some(identity));
            var cbsNode = Mock.Of<ICbsNode>(c => c.GetAmqpAuthentication() == Task.FromResult(amqpAuthentication));
            var amqpConnection = Mock.Of<IAmqpConnection>(c => c.FindExtension<ICbsNode>() == cbsNode);
            var connectionHandler = new ConnectionHandler(amqpConnection, connectionProvider);

            DirectMethodRequest receivedRequest = null;
            Task Handler(DirectMethodRequest request)
            {
                receivedRequest = request;
                return Task.CompletedTask;
            }

            var sentRequest = new DirectMethodRequest(identity.Id, "poke", new byte[0], TimeSpan.FromSeconds(10));

            // Act
            await connectionHandler.GetDeviceListener();
            connectionHandler.RegisterMethodInvoker(Handler);
            await deviceProxy.InvokeMethodAsync(sentRequest);

            // Assert
            Assert.NotNull(receivedRequest);
            Assert.Equal(sentRequest, receivedRequest);
        }

        [Fact]
        public async Task RegisterDesiredPropertiesUpdateSenderTest()
        {
            // Arrange
            IDeviceProxy deviceProxy = null;
            var identity = Mock.Of<IIdentity>(i => i.Id == "d1/m1");
            var deviceListener = Mock.Of<IDeviceListener>();
            Mock.Get(deviceListener).Setup(d => d.BindDeviceProxy(It.IsAny<IDeviceProxy>()))
                .Callback<IDeviceProxy>(d => deviceProxy = d);

            var connectionProvider = Mock.Of<IConnectionProvider>(c => c.GetDeviceListenerAsync(identity) == Task.FromResult(deviceListener));

            var amqpAuthentication = new AmqpAuthentication(true, Option.Some(identity));
            var cbsNode = Mock.Of<ICbsNode>(c => c.GetAmqpAuthentication() == Task.FromResult(amqpAuthentication));
            var amqpConnection = Mock.Of<IAmqpConnection>(c => c.FindExtension<ICbsNode>() == cbsNode);
            var connectionHandler = new ConnectionHandler(amqpConnection, connectionProvider);

            IMessage receivedMessage = null;
            Task Handler(IMessage message)
            {
                receivedMessage = message;
                return Task.CompletedTask;
            }

            var messageToSend = Mock.Of<IMessage>();

            // Act
            await connectionHandler.GetDeviceListener();
            connectionHandler.RegisterDesiredPropertiesUpdateSender(Handler);
            await deviceProxy.OnDesiredPropertyUpdates(messageToSend);

            // Assert
            Assert.NotNull(receivedMessage);
            Assert.Equal(messageToSend, receivedMessage);
        }
    }
}
