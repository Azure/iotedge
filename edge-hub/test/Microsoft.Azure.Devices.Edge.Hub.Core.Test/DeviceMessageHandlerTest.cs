// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Unit]
    public class DeviceMessageHandlerTest
    {
        [Fact]
        public async Task ForwardsGetTwinOperationToEdgeHub()
        {
            var edgeHub = new Mock<IEdgeHub>();
            var connMgr = Mock.Of<IConnectionManager>();
            var identity = Mock.Of<IDeviceIdentity>(i => i.Id == "d1");
            var cloudProxy = Mock.Of<ICloudProxy>();
            var deviceProxy = new Mock<IDeviceProxy>();
            IMessage actualMessage = null;
            deviceProxy.Setup(d => d.SendTwinUpdate(It.IsAny<IMessage>()))
                .Callback<IMessage>(m => actualMessage = m)
                .Returns(Task.CompletedTask);

            IMessage expectedMessage = new EdgeMessage.Builder(new byte[0]).Build();
            edgeHub.Setup(e => e.GetTwinAsync(It.IsAny<string>())).Returns(Task.FromResult(expectedMessage));
            Mock.Get(connMgr).Setup(c => c.GetCloudConnection(It.IsAny<string>())).Returns(Task.FromResult(Option.Some(cloudProxy)));
            var listener = new DeviceMessageHandler(identity, edgeHub.Object, connMgr);
            listener.BindDeviceProxy(deviceProxy.Object);
            await listener.SendGetTwinRequest("cid");

            edgeHub.Verify(x => x.GetTwinAsync(identity.Id), Times.Once);
            Assert.Same(expectedMessage, actualMessage);
        }

        [Fact]
        public async Task ProcessMessageBatchAsync_RouteAsyncTest()
        {
            var cloudProxy = Mock.Of<ICloudProxy>();
            var connectionManager = Mock.Of<IConnectionManager>();
            var edgeHub = Mock.Of<IEdgeHub>();
            var identity = Mock.Of<IDeviceIdentity>();
            var messages = new List<IMessage>();
            messages.Add(Mock.Of<IMessage>());
            messages.Add(Mock.Of<IMessage>());
            Mock.Get(connectionManager).Setup(c => c.GetCloudConnection(It.IsAny<string>())).Returns(Task.FromResult(Option.Some(cloudProxy)));
            var deviceListener = new DeviceMessageHandler(identity, edgeHub, connectionManager);
            await deviceListener.ProcessDeviceMessageBatchAsync(messages);

            Mock.Get(edgeHub).Verify(eh => eh.ProcessDeviceMessageBatch(identity, It.IsAny<IEnumerable<IMessage>>()), Times.Once());
        }

        [Fact]
        public async Task ProcessFeedbackMessageAsync_RouteAsyncTest()
        {
            var cloudProxy = Mock.Of<ICloudProxy>();
            var connectionManager = Mock.Of<IConnectionManager>();
            var edgeHub = Mock.Of<IEdgeHub>();
            var identity = Mock.Of<IDeviceIdentity>();
            string messageId = "messageId";
            var status = FeedbackStatus.Complete;
            Mock.Get(connectionManager).Setup(c => c.GetCloudConnection(It.IsAny<string>())).Returns(Task.FromResult(Option.Some(cloudProxy)));
            var deviceListener = new DeviceMessageHandler(identity, edgeHub, connectionManager);
            await deviceListener.ProcessMessageFeedbackAsync(messageId, status);

            Mock.Get(cloudProxy).Verify(cp => cp.SendFeedbackMessageAsync(messageId, status), Times.Once());
        }

        [Fact]
        public async Task ForwardsTwinPatchOperationToTheCloudProxy()
        {
            var connMgr = Mock.Of<IConnectionManager>();
            var identity = Mock.Of<IModuleIdentity>(m => m.DeviceId == "device1" && m.ModuleId == "module1" && m.Id == "device1/module1");
            var cloudProxy = Mock.Of<ICloudProxy>();

            IMessage receivedMessage = null;
            var edgeHub = new Mock<IEdgeHub>();
            edgeHub.Setup(e => e.UpdateReportedPropertiesAsync(It.IsAny<IIdentity>(), It.IsAny<IMessage>()))
                .Callback<IIdentity, IMessage>((id, m) => receivedMessage = m)
                .Returns(Task.CompletedTask);
            Mock.Get(connMgr).Setup(c => c.GetCloudConnection(It.IsAny<string>())).Returns(Task.FromResult(Option.Some(cloudProxy)));
            var listener = new DeviceMessageHandler(identity, edgeHub.Object, connMgr);
            var underlyingDeviceProxy = new Mock<IDeviceProxy>();
            bool updateSent = false;
            underlyingDeviceProxy.Setup(d => d.SendTwinUpdate(It.IsAny<IMessage>()))
                .Callback(() => updateSent = true)
                .Returns(Task.CompletedTask);
            listener.BindDeviceProxy(underlyingDeviceProxy.Object);
            IMessage message = new EdgeMessage.Builder(Encoding.UTF8.GetBytes("don't care")).Build();
            await listener.UpdateReportedPropertiesAsync(message, Guid.NewGuid().ToString());

            edgeHub.VerifyAll();
            Assert.True(updateSent);
            Assert.NotNull(receivedMessage);
            Assert.Equal(Constants.TwinChangeNotificationMessageSchema, receivedMessage.SystemProperties[SystemProperties.MessageSchema]);
            Assert.Equal(Constants.TwinChangeNotificationMessageType, receivedMessage.SystemProperties[SystemProperties.MessageType]);
            Assert.Equal("device1", receivedMessage.SystemProperties[SystemProperties.ConnectionDeviceId]);
            Assert.Equal("module1", receivedMessage.SystemProperties[SystemProperties.ConnectionModuleId]);
            Assert.True(receivedMessage.SystemProperties.ContainsKey(SystemProperties.EnqueuedTime));
        }

        [Fact]
        public async Task InvokeMethodTest()
        {
            DeviceMessageHandler deviceMessageHandler = this.GetDeviceMessageHandler();
            var methodRequest = new DirectMethodRequest("device10", "shutdown", null, TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(10));

            Task<DirectMethodResponse> responseTask = deviceMessageHandler.InvokeMethodAsync(methodRequest);
            Assert.False(responseTask.IsCompleted);

            IMessage message = new EdgeMessage.Builder(new byte[0]).Build();
            message.Properties[SystemProperties.CorrelationId] = methodRequest.CorrelationId;
            message.Properties[SystemProperties.StatusCode] = "200";
            await deviceMessageHandler.ProcessMethodResponseAsync(message);

            Assert.True(responseTask.IsCompleted);
            Assert.Equal(methodRequest.CorrelationId, responseTask.Result.CorrelationId);
            Assert.Equal(200, responseTask.Result.Status);
        }

        [Fact]
        public async Task InvokeMethodTimeoutTest()
        {
            DeviceMessageHandler deviceMessageHandler = this.GetDeviceMessageHandler();
            var methodRequest = new DirectMethodRequest("device10", "shutdown", null, TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(10));

            Task<DirectMethodResponse> responseTask = deviceMessageHandler.InvokeMethodAsync(methodRequest);
            Assert.False(responseTask.IsCompleted);

            await Task.Delay(TimeSpan.FromSeconds(5));
            Assert.True(responseTask.IsCompleted);
            Assert.NotNull(responseTask.Result);
            Assert.Null(responseTask.Result.Data);
        }

        [Fact]
        public async Task InvokedMethodMismatchedResponseTest()
        {
            DeviceMessageHandler deviceMessageHandler = this.GetDeviceMessageHandler();
            var methodRequest = new DirectMethodRequest("device10", "shutdown", null, TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(10));

            Task<DirectMethodResponse> responseTask = deviceMessageHandler.InvokeMethodAsync(methodRequest);
            Assert.False(responseTask.IsCompleted);

            IMessage message = new EdgeMessage.Builder(new byte[0]).Build();
            message.Properties[SystemProperties.CorrelationId] = methodRequest.CorrelationId + 1;
            message.Properties[SystemProperties.StatusCode] = "200";
            await deviceMessageHandler.ProcessMethodResponseAsync(message);

            Assert.False(responseTask.IsCompleted);
        }

        [Fact]
        public async Task MessageCompletionTest()
        {
            var connMgr = new Mock<IConnectionManager>();
            connMgr.Setup(c => c.AddDeviceConnection(It.IsAny<IIdentity>(), It.IsAny<IDeviceProxy>()));
            var identity = Mock.Of<IModuleIdentity>(m => m.DeviceId == "device1" && m.ModuleId == "module1" && m.Id == "device1/module1");
            var cloudProxy = new Mock<ICloudProxy>();
            var edgeHub = Mock.Of<IEdgeHub>();
            var underlyingDeviceProxy = new Mock<IDeviceProxy>();
            IMessage receivedMessage = null;
            underlyingDeviceProxy.Setup(d => d.SendMessageAsync(It.IsAny<IMessage>(), It.IsAny<string>()))
                .Callback<IMessage, string>((m, s) => receivedMessage = m)
                .Returns(Task.CompletedTask);
            connMgr.Setup(c => c.GetCloudConnection(It.IsAny<string>())).Returns(Task.FromResult(Option.Some(cloudProxy.Object)));
            var deviceMessageHandler = new DeviceMessageHandler(identity, edgeHub, connMgr.Object);
            deviceMessageHandler.BindDeviceProxy(underlyingDeviceProxy.Object);

            IMessage message = new EdgeMessage.Builder(new byte[0]).Build();
            Task sendMessageTask = deviceMessageHandler.SendMessageAsync(message, "input1");
            Assert.False(sendMessageTask.IsCompleted);

            string messageId = receivedMessage.SystemProperties[SystemProperties.LockToken];
            await deviceMessageHandler.ProcessMessageFeedbackAsync(messageId, FeedbackStatus.Complete);

            Assert.True(sendMessageTask.IsCompleted);
            Assert.False(sendMessageTask.IsFaulted);
        }

        [Fact]
        public async Task MessageCompletionTimeoutTest()
        {
            var connMgr = new Mock<IConnectionManager>();
            connMgr.Setup(c => c.AddDeviceConnection(It.IsAny<IIdentity>(), It.IsAny<IDeviceProxy>()));
            var identity = Mock.Of<IModuleIdentity>(m => m.DeviceId == "device1" && m.ModuleId == "module1" && m.Id == "device1/module1");
            var cloudProxy = new Mock<ICloudProxy>();
            var edgeHub = Mock.Of<IEdgeHub>();
            var underlyingDeviceProxy = new Mock<IDeviceProxy>();
            underlyingDeviceProxy.Setup(d => d.SendMessageAsync(It.IsAny<IMessage>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            connMgr.Setup(c => c.GetCloudConnection(It.IsAny<string>())).Returns(Task.FromResult(Option.Some(cloudProxy.Object)));
            var deviceMessageHandler = new DeviceMessageHandler(identity, edgeHub, connMgr.Object);
            deviceMessageHandler.BindDeviceProxy(underlyingDeviceProxy.Object);

            IMessage message = new EdgeMessage.Builder(new byte[0]).Build();
            Task sendMessageTask = deviceMessageHandler.SendMessageAsync(message, "input1");
            Assert.False(sendMessageTask.IsCompleted);

            await Assert.ThrowsAsync<TimeoutException>(async () => await sendMessageTask);
        }

        [Fact]
        public async Task MessageCompletionMismatchedResponseTest()
        {
            var connMgr = new Mock<IConnectionManager>();
            connMgr.Setup(c => c.AddDeviceConnection(It.IsAny<IIdentity>(), It.IsAny<IDeviceProxy>()));
            var identity = Mock.Of<IModuleIdentity>(m => m.DeviceId == "device1" && m.ModuleId == "module1" && m.Id == "device1/module1");
            var cloudProxy = new Mock<ICloudProxy>();
            var edgeHub = Mock.Of<IEdgeHub>();
            var underlyingDeviceProxy = new Mock<IDeviceProxy>();
            underlyingDeviceProxy.Setup(d => d.SendMessageAsync(It.IsAny<IMessage>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            connMgr.Setup(c => c.GetCloudConnection(It.IsAny<string>())).Returns(Task.FromResult(Option.Some(cloudProxy.Object)));
            var deviceMessageHandler = new DeviceMessageHandler(identity, edgeHub, connMgr.Object);
            deviceMessageHandler.BindDeviceProxy(underlyingDeviceProxy.Object);

            IMessage message = new EdgeMessage.Builder(new byte[0]).Build();
            Task sendMessageTask = deviceMessageHandler.SendMessageAsync(message, "input1");
            Assert.False(sendMessageTask.IsCompleted);

            await deviceMessageHandler.ProcessMessageFeedbackAsync(Guid.NewGuid().ToString(), FeedbackStatus.Complete);

            Assert.False(sendMessageTask.IsCompleted);
        }

        [Fact]
        public async Task X509DeviceCanSendMessageTest()
        {
            var connMgr = new Mock<IConnectionManager>();
            connMgr.Setup(c => c.AddDeviceConnection(It.IsAny<IIdentity>(), It.IsAny<IDeviceProxy>()));
            var identity = Mock.Of<IModuleIdentity>(m => m.DeviceId == "device1" && m.ModuleId == "module1" && m.Id == "device1/module1");
            var edgeHub = Mock.Of<IEdgeHub>();
            Option<ICloudProxy> cloudProxy = Option.None<ICloudProxy>();
            bool messageReceived = false;
            string lockToken = null;
            var underlyingDeviceProxy = new Mock<IDeviceProxy>();
            underlyingDeviceProxy.Setup(d => d.SendMessageAsync(It.IsAny<IMessage>(), It.IsAny<string>()))
                .Callback<IMessage, string>(
                    (m, i) =>
                    {
                        messageReceived = true;
                        lockToken = m.SystemProperties[SystemProperties.LockToken];
                    })
                .Returns(Task.CompletedTask);
            connMgr.Setup(c => c.GetCloudConnection(It.IsAny<string>())).Returns(Task.FromResult(cloudProxy));
            var deviceMessageHandler = new DeviceMessageHandler(identity, edgeHub, connMgr.Object);
            deviceMessageHandler.BindDeviceProxy(underlyingDeviceProxy.Object);

            IMessage message = new EdgeMessage.Builder(new byte[0]).Build();
            // send message to x509 device
            Task sendMessageTask = deviceMessageHandler.SendMessageAsync(message, "input1");
            await deviceMessageHandler.ProcessMessageFeedbackAsync(lockToken, FeedbackStatus.Complete);
            Assert.True(messageReceived);
            Assert.True(sendMessageTask.IsCompletedSuccessfully);
        }

        [Fact]
        public async Task ProcessDesiredPropertiesUpdateSubscription()
        {
            // Arrange
            var edgeHub = new Mock<IEdgeHub>();
            edgeHub.Setup(e => e.AddSubscription("d1", DeviceSubscription.DesiredPropertyUpdates)).Returns(Task.CompletedTask);
            var connMgr = Mock.Of<IConnectionManager>();
            var identity = Mock.Of<IDeviceIdentity>(i => i.Id == "d1");
            var deviceProxy = new Mock<IDeviceProxy>();
            IMessage sentMessage = null;
            deviceProxy.Setup(d => d.SendTwinUpdate(It.IsAny<IMessage>()))
                .Callback<IMessage>(m => sentMessage = m)
                .Returns(Task.CompletedTask);

            var listener = new DeviceMessageHandler(identity, edgeHub.Object, connMgr);
            listener.BindDeviceProxy(deviceProxy.Object);
            string correlationId = Guid.NewGuid().ToString();

            // Act
            await listener.AddDesiredPropertyUpdatesSubscription(correlationId);

            // Assert
            Assert.NotNull(sentMessage);
            Assert.Equal(correlationId, sentMessage.SystemProperties[SystemProperties.CorrelationId]);
            Assert.Equal("200", sentMessage.SystemProperties[SystemProperties.StatusCode]);
            edgeHub.VerifyAll();
        }

        [Fact]
        public async Task ProcessRemoveDesiredPropertiesUpdateSubscription()
        {
            // Arrange
            var edgeHub = new Mock<IEdgeHub>();
            edgeHub.Setup(e => e.RemoveSubscription("d1", DeviceSubscription.DesiredPropertyUpdates))
                .Returns(Task.CompletedTask);
            var connMgr = Mock.Of<IConnectionManager>();
            var identity = Mock.Of<IDeviceIdentity>(i => i.Id == "d1");
            var deviceProxy = new Mock<IDeviceProxy>();
            IMessage sentMessage = null;
            deviceProxy.Setup(d => d.SendTwinUpdate(It.IsAny<IMessage>()))
                .Callback<IMessage>(m => sentMessage = m)
                .Returns(Task.CompletedTask);

            var listener = new DeviceMessageHandler(identity, edgeHub.Object, connMgr);
            listener.BindDeviceProxy(deviceProxy.Object);
            string correlationId = Guid.NewGuid().ToString();

            // Act
            await listener.RemoveDesiredPropertyUpdatesSubscription(correlationId);

            // Assert
            Assert.NotNull(sentMessage);
            Assert.Equal(correlationId, sentMessage.SystemProperties[SystemProperties.CorrelationId]);
            Assert.Equal("200", sentMessage.SystemProperties[SystemProperties.StatusCode]);
            edgeHub.VerifyAll();
        }

        DeviceMessageHandler GetDeviceMessageHandler()
        {
            var identity = Mock.Of<IModuleIdentity>(m => m.DeviceId == "device1" && m.ModuleId == "module1" && m.Id == "device1/module1");
            var cloudProxy = new Mock<ICloudProxy>();
            var edgeHub = Mock.Of<IEdgeHub>();
            var underlyingDeviceProxy = new Mock<IDeviceProxy>();
            underlyingDeviceProxy.Setup(d => d.InvokeMethodAsync(It.IsAny<DirectMethodRequest>())).ReturnsAsync(default(DirectMethodResponse));
            underlyingDeviceProxy.Setup(d => d.SendC2DMessageAsync(It.IsAny<IMessage>())).Returns(Task.CompletedTask);
            var connMgr = new Mock<IConnectionManager>();
            connMgr.Setup(c => c.AddDeviceConnection(It.IsAny<IIdentity>(), It.IsAny<IDeviceProxy>()));
            connMgr.Setup(c => c.GetCloudConnection(It.IsAny<string>())).Returns(Task.FromResult(Option.Some(cloudProxy.Object)));
            var deviceMessageHandler = new DeviceMessageHandler(identity, edgeHub, connMgr.Object);
            deviceMessageHandler.BindDeviceProxy(underlyingDeviceProxy.Object);
            return deviceMessageHandler;
        }
    }
}
