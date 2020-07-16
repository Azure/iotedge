// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter.Test
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Unit]
    public class DeviceProxyTests
    {
        [Fact]
        public async Task DirectMethodCallForwarded()
        {
            var twinHandler = Mock.Of<ITwinHandler>();
            var m2mHandler = Mock.Of<IModuleToModuleMessageHandler>();
            var c2dHandler = Mock.Of<ICloud2DeviceMessageHandler>();
            var directMethodHandler = Mock.Of<IDirectMethodHandler>();
            var identity = new DeviceIdentity("hub", "device_id");

            var request = new DirectMethodRequest("123", "name", new byte[] { 1, 2, 3 }, TimeSpan.FromSeconds(60));

            Mock.Get(directMethodHandler)
                .Setup(h => h.CallDirectMethodAsync(It.Is<DirectMethodRequest>(m => m == request), It.Is<IIdentity>(i => i == identity)))
                .Returns(Task.FromResult(new DirectMethodResponse("123", new byte[0], 200)));

            var sut = new DeviceProxy(identity, twinHandler, m2mHandler, c2dHandler, directMethodHandler);

            await sut.InvokeMethodAsync(request);

            Mock.Get(directMethodHandler).VerifyAll();
        }

        [Fact]
        public async Task DesiredUpdateCallForwarded()
        {
            var twinHandler = Mock.Of<ITwinHandler>();
            var m2mHandler = Mock.Of<IModuleToModuleMessageHandler>();
            var c2dHandler = Mock.Of<ICloud2DeviceMessageHandler>();
            var directMethodHandler = Mock.Of<IDirectMethodHandler>();
            var identity = new DeviceIdentity("hub", "device_id");

            var desired = new EdgeMessage.Builder(new byte[0]).Build();

            Mock.Get(twinHandler)
                .Setup(h => h.SendDesiredPropertiesUpdate(It.Is<IMessage>(m => m == desired), It.Is<IIdentity>(i => i == identity)))
                .Returns(Task.CompletedTask);

            var sut = new DeviceProxy(identity, twinHandler, m2mHandler, c2dHandler, directMethodHandler);

            await sut.OnDesiredPropertyUpdates(desired);

            Mock.Get(directMethodHandler).VerifyAll();
        }

        [Fact]
        public async Task Cloud2DeviceMessagesForwarded()
        {
            var twinHandler = Mock.Of<ITwinHandler>();
            var m2mHandler = Mock.Of<IModuleToModuleMessageHandler>();
            var c2dHandler = Mock.Of<ICloud2DeviceMessageHandler>();
            var directMethodHandler = Mock.Of<IDirectMethodHandler>();
            var identity = new DeviceIdentity("hub", "device_id");

            var message = new EdgeMessage.Builder(new byte[0]).Build();

            Mock.Get(c2dHandler)
                .Setup(h => h.SendC2DMessageAsync(It.Is<IMessage>(m => m == message), It.Is<IIdentity>(i => i == identity)))
                .Returns(Task.CompletedTask);

            var sut = new DeviceProxy(identity, twinHandler, m2mHandler, c2dHandler, directMethodHandler);

            await sut.SendC2DMessageAsync(message);

            Mock.Get(directMethodHandler).VerifyAll();
        }

        [Fact]
        public async Task Moudle2MoudleMessagesForwarded()
        {
            var twinHandler = Mock.Of<ITwinHandler>();
            var m2mHandler = Mock.Of<IModuleToModuleMessageHandler>();
            var c2dHandler = Mock.Of<ICloud2DeviceMessageHandler>();
            var directMethodHandler = Mock.Of<IDirectMethodHandler>();
            var identity = new DeviceIdentity("hub", "device_id");

            var input = "input";
            var message = new EdgeMessage.Builder(new byte[0]).Build();

            Mock.Get(m2mHandler)
                .Setup(h => h.SendModuleToModuleMessageAsync(It.Is<IMessage>(m => m == message), It.Is<string>(s => s == input), It.Is<IIdentity>(i => i == identity)))
                .Returns(Task.CompletedTask);

            var sut = new DeviceProxy(identity, twinHandler, m2mHandler, c2dHandler, directMethodHandler);

            await sut.SendMessageAsync(message, input);

            Mock.Get(directMethodHandler).VerifyAll();
        }

        [Fact]
        public async Task TwinUpdatesForwarded()
        {
            var twinHandler = Mock.Of<ITwinHandler>();
            var m2mHandler = Mock.Of<IModuleToModuleMessageHandler>();
            var c2dHandler = Mock.Of<ICloud2DeviceMessageHandler>();
            var directMethodHandler = Mock.Of<IDirectMethodHandler>();
            var identity = new DeviceIdentity("hub", "device_id");

            var twin = new EdgeMessage.Builder(new byte[0]).Build();

            Mock.Get(twinHandler)
                .Setup(h => h.SendTwinUpdate(It.Is<IMessage>(m => m == twin), It.Is<IIdentity>(i => i == identity)))
                .Returns(Task.CompletedTask);

            var sut = new DeviceProxy(identity, twinHandler, m2mHandler, c2dHandler, directMethodHandler);

            await sut.SendTwinUpdate(twin);

            Mock.Get(directMethodHandler).VerifyAll();
        }

        [Fact]
        public void SetInactiveMakesInactive()
        {
            var twinHandler = Mock.Of<ITwinHandler>();
            var m2mHandler = Mock.Of<IModuleToModuleMessageHandler>();
            var c2dHandler = Mock.Of<ICloud2DeviceMessageHandler>();
            var directMethodHandler = Mock.Of<IDirectMethodHandler>();
            var identity = new DeviceIdentity("hub", "device_id");

            var sut = new DeviceProxy(identity, twinHandler, m2mHandler, c2dHandler, directMethodHandler);

            Assert.True(sut.IsActive);

            sut.SetInactive();

            Assert.False(sut.IsActive);
        }
    }
}
