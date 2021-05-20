// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Amqp;
    using Microsoft.Azure.Amqp.Framing;
    using Microsoft.Azure.Amqp.Transport;
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
        public void ConnectionHandlerCtorTest()
        {
            // Arrange
            var identity = Mock.Of<IIdentity>();
            var connectionProvider = Mock.Of<IConnectionProvider>();
            var amqpConnection = new AmqpTestConnection();

            // Act / Assert
            Assert.NotNull(new ClientConnectionHandler(identity, connectionProvider, amqpConnection));
            Assert.Throws<ArgumentNullException>(() => new ClientConnectionHandler(null, connectionProvider, amqpConnection));
            Assert.Throws<ArgumentNullException>(() => new ClientConnectionHandler(identity, null, amqpConnection));
            Assert.Throws<ArgumentNullException>(() => new ClientConnectionHandler(identity, connectionProvider, null));
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

            var connectionProvider = Mock.Of<IConnectionProvider>(c => c.GetDeviceListenerAsync(identity, Option.None<string>()) == Task.FromResult(deviceListener));
            var amqpConnection = new AmqpTestConnection();

            var connectionHandler = new ClientConnectionHandler(identity, connectionProvider, amqpConnection);

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
            Mock.Get(connectionProvider).Verify(c => c.GetDeviceListenerAsync(It.IsAny<IIdentity>(), Option.None<string>()), Times.Once);
            Mock.Get(deviceListener).Verify(d => d.BindDeviceProxy(It.IsAny<IDeviceProxy>()), Times.Once);
        }

        [Fact]
        public async Task RegisterC2DMessageSenderTest()
        {
            // Arrange
            IDeviceProxy deviceProxy = null;
            var identity = Mock.Of<IIdentity>(i => i.Id == "d1");
            var deviceListener = Mock.Of<IDeviceListener>();
            Mock.Get(deviceListener).Setup(d => d.BindDeviceProxy(It.IsAny<IDeviceProxy>()))
                .Callback<IDeviceProxy>(d => deviceProxy = d);

            var connectionProvider = Mock.Of<IConnectionProvider>(c => c.GetDeviceListenerAsync(identity, Option.None<string>()) == Task.FromResult(deviceListener));
            var amqpConnection = new AmqpTestConnection();

            var connectionHandler = new ClientConnectionHandler(identity, connectionProvider, amqpConnection);

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
            Assert.Equal("/devices/d1", systemProperties[SystemProperties.To]);
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

            var connectionProvider = Mock.Of<IConnectionProvider>(c => c.GetDeviceListenerAsync(identity, Option.None<string>()) == Task.FromResult(deviceListener));
            var amqpConnection = new AmqpTestConnection();

            var connectionHandler = new ClientConnectionHandler(identity, connectionProvider, amqpConnection);

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
            Assert.Equal("i1", systemProperties[SystemProperties.InputName]);
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

            var connectionProvider = Mock.Of<IConnectionProvider>(c => c.GetDeviceListenerAsync(identity, Option.None<string>()) == Task.FromResult(deviceListener));
            var amqpConnection = new AmqpTestConnection();

            var connectionHandler = new ClientConnectionHandler(identity, connectionProvider, amqpConnection);

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
        public async Task RegisterDesiredPropertiesUpdateSenderTest()
        {
            // Arrange
            IDeviceProxy deviceProxy = null;
            var identity = Mock.Of<IIdentity>(i => i.Id == "d1/m1");
            var deviceListener = Mock.Of<IDeviceListener>();
            Mock.Get(deviceListener).Setup(d => d.BindDeviceProxy(It.IsAny<IDeviceProxy>()))
                .Callback<IDeviceProxy>(d => deviceProxy = d);

            var connectionProvider = Mock.Of<IConnectionProvider>(c => c.GetDeviceListenerAsync(identity, Option.None<string>()) == Task.FromResult(deviceListener));
            var amqpConnection = new AmqpTestConnection();

            var connectionHandler = new ClientConnectionHandler(identity, connectionProvider, amqpConnection);

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
        public async Task CloseOnRemovingAllLinksTest()
        {
            // Arrange
            var deviceListener = new Mock<IDeviceListener>();
            deviceListener.Setup(d => d.CloseAsync()).Returns(Task.CompletedTask);
            deviceListener.Setup(d => d.BindDeviceProxy(It.IsAny<IDeviceProxy>()));

            var identity = Mock.Of<IIdentity>(i => i.Id == "d1/m1");
            var connectionProvider = Mock.Of<IConnectionProvider>(c => c.GetDeviceListenerAsync(identity, Option.None<string>()) == Task.FromResult(deviceListener.Object));
            var amqpConnection = new AmqpTestConnection();

            var connectionHandler = new ClientConnectionHandler(identity, connectionProvider, amqpConnection);

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
            Assert.True(amqpConnection.CloseCalled);

            // Act
            await connectionHandler.GetDeviceListener();

            // Assert
            deviceListener.Verify(d => d.BindDeviceProxy(It.IsAny<IDeviceProxy>()), Times.Exactly(2));
        }
    }

    class AmqpTestConnection : AmqpConnectionBase
    {
        public AmqpTestConnection()
            : base("test", new TestTransport(), new AmqpConnectionSettings(), false)
        {
        }

        public bool CloseCalled { get; private set; }

        protected override void AbortInternal()
        {
        }

        protected override bool CloseInternal()
        {
            this.CloseCalled = true;
            return true;
        }

        protected override void OnFrameBuffer(ByteBuffer buffer)
        {
        }

        protected override void OnProtocolHeader(ProtocolHeader header)
        {
        }

        protected override bool OpenInternal() => true;
    }

    class TestTransport : TransportBase
    {
        public override string LocalEndPoint => "localhost";
        public override string RemoteEndPoint => "remotehost";

        public TestTransport()
            : base("test")
        {
        }

        public bool CloseCalled { get; private set; }

        public override bool ReadAsync(TransportAsyncCallbackArgs args) => true;

        public override void SetMonitor(ITransportMonitor usageMeter)
        {
        }

        public override bool WriteAsync(TransportAsyncCallbackArgs args) => false;

        protected override void AbortInternal()
        {
        }

        protected override bool CloseInternal() => true;
    }
}
