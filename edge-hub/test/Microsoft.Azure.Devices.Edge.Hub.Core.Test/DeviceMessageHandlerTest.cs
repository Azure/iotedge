// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;
    using IMessage = Microsoft.Azure.Devices.Edge.Hub.Core.IMessage;

    [Unit]
    public class DeviceMessageHandlerTest
    {
        [Fact]
        public async Task ForwardsGetTwinOperationToTheCloudProxy()
        {
            var edgeHub = Mock.Of<IEdgeHub>();
            var connMgr = Mock.Of<IConnectionManager>();
            var identity = Mock.Of<IDeviceIdentity>();

            IMessage expectedMessage = new Message(new byte[0]);

            var cloudProxy = new Mock<ICloudProxy>();
            cloudProxy.Setup(x => x.GetTwinAsync())
                .Returns(Task.FromResult(expectedMessage));

            var listener = new DeviceMessageHandler(identity, edgeHub, connMgr, cloudProxy.Object);
            IMessage actualMessage = await listener.GetTwinAsync();

            cloudProxy.Verify(x => x.GetTwinAsync(), Times.Once);
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

            var deviceListener = new DeviceMessageHandler(identity, edgeHub, connectionManager, cloudProxy);
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
            FeedbackStatus status = FeedbackStatus.Complete;

            var deviceListener = new DeviceMessageHandler(identity, edgeHub, connectionManager, cloudProxy);
            await deviceListener.ProcessMessageFeedbackAsync(messageId, status);

            Mock.Get(cloudProxy).Verify(cp => cp.SendFeedbackMessageAsync(messageId, status), Times.Once());
        }

        [Fact]
        public async Task ForwardsTwinPatchOperationToTheCloudProxy()
        {            
            var connMgr = Mock.Of<IConnectionManager>();
            var identity = Mock.Of<IModuleIdentity>(m => m.DeviceId == "device1" && m.ModuleId == "module1");
            var cloudProxy = Mock.Of<ICloudProxy>();

            IMessage receivedMessage = null;
            var edgeHub = new Mock<IEdgeHub>();
            edgeHub.Setup(e => e.UpdateReportedPropertiesAsync(It.IsAny<IIdentity>(), It.IsAny<IMessage>()))
                .Callback<IIdentity, IMessage>((id, m) => receivedMessage = m)
                .Returns(Task.CompletedTask);

            var listener = new DeviceMessageHandler(identity, edgeHub.Object, connMgr, cloudProxy);
            IMessage message = new Message(Encoding.UTF8.GetBytes("don't care"));
            await listener.UpdateReportedPropertiesAsync(message);

            edgeHub.VerifyAll();
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
            var deviceMessageHandler = this.GetDeviceMessageHandler();
            var methodRequest = new DirectMethodRequest("device10", "shutdown", null, TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(10));

            Task<DirectMethodResponse> responseTask = deviceMessageHandler.InvokeMethodAsync(methodRequest);
            Assert.False(responseTask.IsCompleted);

            IMessage message = new Message(new byte[0]);
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
            var deviceMessageHandler = this.GetDeviceMessageHandler();
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
            var deviceMessageHandler = this.GetDeviceMessageHandler();            
            var methodRequest = new DirectMethodRequest("device10", "shutdown", null, TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(10));

            Task<DirectMethodResponse> responseTask = deviceMessageHandler.InvokeMethodAsync(methodRequest);
            Assert.False(responseTask.IsCompleted);

            IMessage message = new Message(new byte[0]);
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
            var identity = Mock.Of<IModuleIdentity>(m => m.DeviceId == "device1" && m.ModuleId == "module1");
            var cloudProxy = new Mock<ICloudProxy>();
            cloudProxy.Setup(c => c.BindCloudListener(It.IsAny<ICloudListener>()));
            var edgeHub = Mock.Of<IEdgeHub>();
            var underlyingDeviceProxy = new Mock<IDeviceProxy>();
            IMessage receivedMessage = null;
            underlyingDeviceProxy.Setup(d => d.SendMessageAsync(It.IsAny<IMessage>(), It.IsAny<string>()))
                .Callback<IMessage, string>((m, s) => receivedMessage = m)
                .Returns(Task.CompletedTask);

            var deviceMessageHandler = new DeviceMessageHandler(identity, edgeHub, connMgr.Object, cloudProxy.Object);
            deviceMessageHandler.BindDeviceProxy(underlyingDeviceProxy.Object);

            IMessage message = new Message(new byte[0]);
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
            var identity = Mock.Of<IModuleIdentity>(m => m.DeviceId == "device1" && m.ModuleId == "module1");
            var cloudProxy = new Mock<ICloudProxy>();
            cloudProxy.Setup(c => c.BindCloudListener(It.IsAny<ICloudListener>()));
            var edgeHub = Mock.Of<IEdgeHub>();
            var underlyingDeviceProxy = new Mock<IDeviceProxy>();
            IMessage receivedMessage = null;
            underlyingDeviceProxy.Setup(d => d.SendMessageAsync(It.IsAny<IMessage>(), It.IsAny<string>()))
                .Callback<IMessage, string>((m, s) => receivedMessage = m)
                .Returns(Task.CompletedTask);

            var deviceMessageHandler = new DeviceMessageHandler(identity, edgeHub, connMgr.Object, cloudProxy.Object);
            deviceMessageHandler.BindDeviceProxy(underlyingDeviceProxy.Object);

            IMessage message = new Message(new byte[0]);
            Task sendMessageTask = deviceMessageHandler.SendMessageAsync(message, "input1");
            Assert.False(sendMessageTask.IsCompleted);

            await Assert.ThrowsAsync<TimeoutException>(async () => await sendMessageTask);
        }

        [Fact]
        public async Task MessageCompletionMismatchedResponseTest()
        {
            var connMgr = new Mock<IConnectionManager>();
            connMgr.Setup(c => c.AddDeviceConnection(It.IsAny<IIdentity>(), It.IsAny<IDeviceProxy>()));
            var identity = Mock.Of<IModuleIdentity>(m => m.DeviceId == "device1" && m.ModuleId == "module1");
            var cloudProxy = new Mock<ICloudProxy>();
            cloudProxy.Setup(c => c.BindCloudListener(It.IsAny<ICloudListener>()));
            var edgeHub = Mock.Of<IEdgeHub>();
            var underlyingDeviceProxy = new Mock<IDeviceProxy>();
            IMessage receivedMessage = null;
            underlyingDeviceProxy.Setup(d => d.SendMessageAsync(It.IsAny<IMessage>(), It.IsAny<string>()))
                .Callback<IMessage, string>((m, s) => receivedMessage = m)
                .Returns(Task.CompletedTask);

            var deviceMessageHandler = new DeviceMessageHandler(identity, edgeHub, connMgr.Object, cloudProxy.Object);
            deviceMessageHandler.BindDeviceProxy(underlyingDeviceProxy.Object);

            IMessage message = new Message(new byte[0]);
            Task sendMessageTask = deviceMessageHandler.SendMessageAsync(message, "input1");
            Assert.False(sendMessageTask.IsCompleted);

            await deviceMessageHandler.ProcessMessageFeedbackAsync(Guid.NewGuid().ToString(), FeedbackStatus.Complete);

            Assert.False(sendMessageTask.IsCompleted);
        }

        DeviceMessageHandler GetDeviceMessageHandler()
        {
            var connMgr = new Mock<IConnectionManager>();
            connMgr.Setup(c => c.AddDeviceConnection(It.IsAny<IIdentity>(), It.IsAny<IDeviceProxy>()));
            var identity = Mock.Of<IModuleIdentity>(m => m.DeviceId == "device1" && m.ModuleId == "module1");
            var cloudProxy = new Mock<ICloudProxy>();
            cloudProxy.Setup(c => c.BindCloudListener(It.IsAny<ICloudListener>()));
            var edgeHub = Mock.Of<IEdgeHub>();
            var underlyingDeviceProxy = new Mock<IDeviceProxy>();
            underlyingDeviceProxy.Setup(d => d.InvokeMethodAsync(It.IsAny<DirectMethodRequest>())).ReturnsAsync(default(DirectMethodResponse));
            underlyingDeviceProxy.Setup(d => d.SendC2DMessageAsync(It.IsAny<IMessage>())).Returns(Task.CompletedTask);

            var deviceMessageHandler = new DeviceMessageHandler(identity, edgeHub, connMgr.Object, cloudProxy.Object);
            deviceMessageHandler.BindDeviceProxy(underlyingDeviceProxy.Object);
            return deviceMessageHandler;
        }
    }
}