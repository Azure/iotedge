// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy.Test
{
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Moq;
    using Xunit;

    public class CloudProxyProviderTest
    {
        [Fact(Skip = "Classes not fully implemented")]
        public void ConnectTest()
        {
            ICloudProxyProvider cloudProxyProvider = new CloudProxyProvider();
            var cloudListenerMock = new Mock<ICloudListener>();

            ICloudProxy cloudProxy = cloudProxyProvider.Connect("", cloudListenerMock.Object);

            Assert.NotNull(cloudProxy);
        }
    }
}
