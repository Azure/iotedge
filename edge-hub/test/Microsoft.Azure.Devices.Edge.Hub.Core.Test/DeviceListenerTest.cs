// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Shared;
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

            var expectedTwin = new Twin();

            var cloudProxy = new Mock<ICloudProxy>();
            cloudProxy.Setup(x => x.GetTwinAsync())
                .Returns(Task.FromResult(expectedTwin));

            var listener = new DeviceListener(identity, edgeHub, connMgr, cloudProxy.Object);
            Twin actualTwin = await listener.GetTwinAsync();

            cloudProxy.Verify(x => x.GetTwinAsync(), Times.Once);
            Assert.Same(expectedTwin, actualTwin);
        }
    }
}