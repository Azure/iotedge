// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

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
        public async Task ForwardsTwinPatchOperationToTheCloudProxy()
        {
            var edgeHub = Mock.Of<IEdgeHub>();
            var connMgr = Mock.Of<IConnectionManager>();
            var identity = Mock.Of<IDeviceIdentity>();

            var cloudProxy = new Mock<ICloudProxy>();
            var listener = new DeviceListener(identity, edgeHub, connMgr, cloudProxy.Object);
            await listener.UpdateReportedPropertiesAsync("don't care");

            cloudProxy.Verify(x => x.UpdateReportedPropertiesAsync(It.IsAny<string>()), Times.Once);
        }
    }
}