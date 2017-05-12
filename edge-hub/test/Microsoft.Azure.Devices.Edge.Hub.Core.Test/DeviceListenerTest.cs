// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Unit]
    public class DeviceListenerTest
    {
        [Fact]
        public async Task TestReceiveMessage()
        {
            var dispatcher = Mock.Of<IDispatcher>();
            var connMgr = Mock.Of<IConnectionManager>();
            var cloudProxy = Mock.Of<ICloudProxy>();            

            IMessage sentMessage = null;
            var router = new Mock<IRouter>();
            router.Setup(r => r.RouteMessage(It.IsAny<IMessage>(), It.IsAny<string>()))
                .Returns(TaskEx.Done)
                .Callback<IMessage, string>((m, s) => sentMessage = m);

            var moduleIdentity = new Mock<IModuleIdentity>();
            moduleIdentity.SetupGet(m => m.ModuleId).Returns("module1");
            moduleIdentity.SetupGet(m => m.DeviceId).Returns("device1");

            var listener = new DeviceListener(moduleIdentity.Object, router.Object, dispatcher, connMgr, cloudProxy);
            
            var rand = new Random();
            var payload = new byte[50];
            rand.NextBytes(payload);
            IMessage message = new Message(payload);

            await listener.ReceiveMessage(message);
            Assert.NotNull(sentMessage);
            Assert.True(sentMessage.Properties.ContainsKey("module-Id"));

            var deviceIdentity = new Mock<IDeviceIdentity>();
            deviceIdentity.SetupGet(m => m.DeviceId).Returns("device1");

            listener = new DeviceListener(deviceIdentity.Object, router.Object, dispatcher, connMgr, cloudProxy);

            message = new Message(payload);
            sentMessage = null;

            await listener.ReceiveMessage(message);
            Assert.NotNull(sentMessage);
            Assert.False(sentMessage.Properties.ContainsKey("module-Id"));
        }

        [Fact]
        public async Task ForwardsGetTwinOperationToTheCloudProxy()
        {
            var dispatcher = Mock.Of<IDispatcher>();
            var connMgr = Mock.Of<IConnectionManager>();
            var router = Mock.Of<IRouter>();
            var identity = Mock.Of<IDeviceIdentity>();

            var expectedTwin = new Twin();

            var cloudProxy = new Mock<ICloudProxy>();
            cloudProxy.Setup(x => x.GetTwin())
                .Returns(Task.FromResult(expectedTwin));

            var listener = new DeviceListener(identity, router, dispatcher, connMgr, cloudProxy.Object);
            Twin actualTwin = await listener.GetTwin();

            cloudProxy.Verify(x => x.GetTwin(), Times.Once);
            Assert.Same(expectedTwin, actualTwin);
        }
    }
}