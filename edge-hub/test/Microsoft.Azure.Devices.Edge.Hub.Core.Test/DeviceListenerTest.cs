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
    public class DeviceListenerTest
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

            var listener = new DeviceListener(identity, edgeHub, connMgr, cloudProxy.Object);
            IMessage actualMessage = await listener.GetTwinAsync();

            cloudProxy.Verify(x => x.GetTwinAsync(), Times.Once);
            Assert.Same(expectedMessage, actualMessage);
        }

        [Fact]
        public async Task ProcessMessageBatchAsync_NullMessages()
        {
            var cloudProxy = Mock.Of<ICloudProxy>();
            var connectionManager = Mock.Of<IConnectionManager>();
            var edgeHub = Mock.Of<IEdgeHub>();
            var identity = Mock.Of<IDeviceIdentity>();

            var deviceListener = new DeviceListener(identity, edgeHub, connectionManager, cloudProxy);
            await Assert.ThrowsAsync<ArgumentNullException>(() => deviceListener.ProcessMessageBatchAsync(null));
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

            var deviceListener = new DeviceListener(identity, edgeHub, connectionManager, cloudProxy);
            await deviceListener.ProcessMessageBatchAsync(messages);

            Mock.Get(edgeHub).Verify(eh => eh.ProcessDeviceMessageBatch(identity, It.IsAny<IEnumerable<IMessage>>()), Times.Once());
        }

        [Fact]
        public async Task ProcessFeedbackMessageAsync_NullMessages()
        {
            var cloudProxy = Mock.Of<ICloudProxy>();
            var connectionManager = Mock.Of<IConnectionManager>();
            var edgeHub = Mock.Of<IEdgeHub>();
            var identity = Mock.Of<IDeviceIdentity>();

            var deviceListener = new DeviceListener(identity, edgeHub, connectionManager, cloudProxy);
            await Assert.ThrowsAsync<ArgumentNullException>(() => deviceListener.ProcessFeedbackMessageAsync(null));
        }

        [Fact]
        public async Task ProcessFeedbackMessageAsync_RouteAsyncTest()
        {
            var cloudProxy = Mock.Of<ICloudProxy>();
            var connectionManager = Mock.Of<IConnectionManager>();
            var edgeHub = Mock.Of<IEdgeHub>();
            var identity = Mock.Of<IDeviceIdentity>();
            var message = Mock.Of<IFeedbackMessage>();

            var deviceListener = new DeviceListener(identity, edgeHub, connectionManager, cloudProxy);
            await deviceListener.ProcessFeedbackMessageAsync(message);

            Mock.Get(cloudProxy).Verify(cp => cp.SendFeedbackMessageAsync(message), Times.Once());
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

            var listener = new DeviceListener(identity, edgeHub.Object, connMgr, cloudProxy);
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
    }
}