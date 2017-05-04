// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    public class ConnectionManagerTest
    {
        [Fact]
        [Integration]
        public async Task DeviceConnectionTest()
        {
            var cloudProviderMock = new Mock<ICloudProxyProvider>();
            IConnectionManager connectionManager = new ConnectionManager(cloudProviderMock.Object);

            var deviceProxyMock1 = new Mock<IDeviceProxy>();
            deviceProxyMock1.SetupGet(dp => dp.IsActive).Returns(true);
            deviceProxyMock1.Setup(dp => dp.Close(It.IsAny<Exception>())).Callback(() => deviceProxyMock1.SetupGet(dp => dp.IsActive).Returns(false));
            
            var deviceProxyMock2 = new Mock<IDeviceProxy>();
            deviceProxyMock2.SetupGet(dp => dp.IsActive).Returns(true);
            deviceProxyMock2.Setup(dp => dp.Close(It.IsAny<Exception>())).Callback(() => deviceProxyMock2.SetupGet(dp => dp.IsActive).Returns(false));

            var deviceIdentityMock = new Mock<IIdentity>();
            deviceIdentityMock.SetupGet(di => di.Id).Returns("Device1");

            Option<IDeviceProxy> returnedDeviceProxy = connectionManager.GetDeviceConnection(deviceIdentityMock.Object.Id);
            Assert.False(returnedDeviceProxy.HasValue);

            connectionManager.AddDeviceConnection(deviceIdentityMock.Object, deviceProxyMock1.Object);
            Assert.True(deviceProxyMock1.Object.IsActive);

            returnedDeviceProxy = connectionManager.GetDeviceConnection(deviceIdentityMock.Object.Id);
            Assert.True(returnedDeviceProxy.HasValue);
            Assert.Equal(deviceProxyMock1.Object, returnedDeviceProxy.OrDefault());

            connectionManager.AddDeviceConnection(deviceIdentityMock.Object, deviceProxyMock2.Object);
            Assert.True(deviceProxyMock2.Object.IsActive);
            Assert.False(deviceProxyMock1.Object.IsActive);

            returnedDeviceProxy = connectionManager.GetDeviceConnection(deviceIdentityMock.Object.Id);
            Assert.True(returnedDeviceProxy.HasValue);
            Assert.Equal(deviceProxyMock2.Object, returnedDeviceProxy.OrDefault());

            bool result = await connectionManager.CloseConnection(deviceIdentityMock.Object.Id);
            Assert.True(result);

            returnedDeviceProxy = connectionManager.GetDeviceConnection(deviceIdentityMock.Object.Id);
            Assert.False(returnedDeviceProxy.HasValue);
        }

        [Fact]
        [Integration]
        public async Task CloudConnectionTest()
        {
            Mock<ICloudProxy> GetCloudProxyMock()
            {
                var cloudProxyMock = new Mock<ICloudProxy>();
                cloudProxyMock.SetupGet(dp => dp.IsActive).Returns(true);
                cloudProxyMock.Setup(dp => dp.CloseAsync()).Returns(Task.FromResult(true)).Callback(() => cloudProxyMock.SetupGet(dp => dp.IsActive).Returns(false));
                return cloudProxyMock;
            }

            var cloudProviderMock = new Mock<ICloudProxyProvider>();
            cloudProviderMock.Setup(p => p.Connect(It.IsAny<string>())).ReturnsAsync(() => Try.Success(GetCloudProxyMock().Object));

            var deviceIdentityMock = new Mock<IIdentity>();
            deviceIdentityMock.SetupGet(di => di.Id).Returns("Device1");

            IConnectionManager connectionManager = new ConnectionManager(cloudProviderMock.Object);

            Option<ICloudProxy> returnedValue = connectionManager.GetCloudConnection(deviceIdentityMock.Object.Id);
            Assert.False(returnedValue.HasValue);

            Try<ICloudProxy> cloudProxy1 = await connectionManager.CreateCloudConnection(deviceIdentityMock.Object);
            Assert.True(cloudProxy1.Success);
            Assert.True(cloudProxy1.Value.IsActive);

            returnedValue = connectionManager.GetCloudConnection(deviceIdentityMock.Object.Id);
            Assert.True(returnedValue.HasValue);
            Assert.Equal(cloudProxy1.Value, returnedValue.OrDefault());

            Try<ICloudProxy> cloudProxy2 = await connectionManager.GetOrCreateCloudConnection(deviceIdentityMock.Object);
            Assert.True(cloudProxy2.Success);
            Assert.True(cloudProxy2.Value.IsActive);
            Assert.Equal(cloudProxy1.Value, cloudProxy2.Value);

            Try<ICloudProxy> cloudProxy3 = await connectionManager.CreateCloudConnection(deviceIdentityMock.Object);
            Assert.True(cloudProxy3.Success);
            Assert.NotEqual(cloudProxy2.Value, cloudProxy3.Value);
            Assert.True(cloudProxy3.Value.IsActive);
            Assert.False(cloudProxy2.Value.IsActive);

            bool result = await connectionManager.CloseConnection(deviceIdentityMock.Object.Id);
            Assert.True(result);

            returnedValue = connectionManager.GetCloudConnection(deviceIdentityMock.Object.Id);
            Assert.False(returnedValue.HasValue);
        }
    }
}