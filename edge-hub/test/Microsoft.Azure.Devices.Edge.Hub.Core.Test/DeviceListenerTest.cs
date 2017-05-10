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

    public class DeviceListenerTest
    {
        [Fact]
        [Unit]
        public async Task TestReceiveMessage()
        {            
            var dispatcher = new Mock<IDispatcher>();
            var connMgr = new Mock<IConnectionManager>();
            var cloudProxy = new Mock<ICloudProxy>();            

            IMessage sentMessage = null;
            var router = new Mock<IRouter>();
            router.Setup(r => r.RouteMessage(It.IsAny<IMessage>(), It.IsAny<string>()))
                .Returns(TaskEx.Done)
                .Callback<IMessage, string>((m, s) => sentMessage = m);

            var moduleIdentity = new Mock<IModuleIdentity>();
            moduleIdentity.SetupGet(m => m.ModuleId).Returns("module1");
            moduleIdentity.SetupGet(m => m.DeviceId).Returns("device1");

            var deviceListner = new DeviceListener(moduleIdentity.Object, router.Object, dispatcher.Object, connMgr.Object, cloudProxy.Object);
            
            var rand = new Random();
            var payload = new byte[50];
            rand.NextBytes(payload);
            IMessage message = new Message(payload);

            await deviceListner.ReceiveMessage(message);
            Assert.NotNull(sentMessage);
            Assert.True(sentMessage.Properties.ContainsKey("module-Id"));

            var deviceIdentity = new Mock<IDeviceIdentity>();
            deviceIdentity.SetupGet(m => m.DeviceId).Returns("device1");

            deviceListner = new DeviceListener(deviceIdentity.Object, router.Object, dispatcher.Object, connMgr.Object, cloudProxy.Object);

            message = new Message(payload);
            sentMessage = null;

            await deviceListner.ReceiveMessage(message);
            Assert.NotNull(sentMessage);
            Assert.False(sentMessage.Properties.ContainsKey("module-Id"));
        }
    }
}