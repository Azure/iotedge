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

    public class ConnectionManagerTest
    {
        static readonly string EdgeDeviceId = "device1";

        [Fact]
        [Integration]
        public async Task DeviceConnectionTest()
        {
            var cloudProviderMock = new Mock<ICloudProxyProvider>();
            IConnectionManager connectionManager = new ConnectionManager(cloudProviderMock.Object, EdgeDeviceId);

            var deviceProxyMock1 = new Mock<IDeviceProxy>();
            deviceProxyMock1.SetupGet(dp => dp.IsActive).Returns(true);
            deviceProxyMock1.Setup(dp => dp.CloseAsync(It.IsAny<Exception>())).Callback(() => deviceProxyMock1.SetupGet(dp => dp.IsActive).Returns(false));

            var deviceProxyMock2 = new Mock<IDeviceProxy>();
            deviceProxyMock2.SetupGet(dp => dp.IsActive).Returns(true);
            deviceProxyMock2.Setup(dp => dp.CloseAsync(It.IsAny<Exception>())).Callback(() => deviceProxyMock2.SetupGet(dp => dp.IsActive).Returns(false));

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

            bool result = await connectionManager.CloseConnectionAsync(deviceIdentityMock.Object.Id);
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
            cloudProviderMock.Setup(p => p.Connect(It.IsAny<IIdentity>())).ReturnsAsync(() => Try.Success(GetCloudProxyMock().Object));

            var deviceIdentityMock = new Mock<IIdentity>();
            deviceIdentityMock.SetupGet(di => di.Id).Returns("Device1");

            IConnectionManager connectionManager = new ConnectionManager(cloudProviderMock.Object, EdgeDeviceId);

            Option<ICloudProxy> returnedValue = connectionManager.GetCloudConnection(deviceIdentityMock.Object.Id);
            Assert.False(returnedValue.HasValue);

            Try<ICloudProxy> cloudProxy1 = await connectionManager.CreateCloudConnectionAsync(deviceIdentityMock.Object);
            Assert.True(cloudProxy1.Success);
            Assert.True(cloudProxy1.Value.IsActive);

            returnedValue = connectionManager.GetCloudConnection(deviceIdentityMock.Object.Id);
            Assert.True(returnedValue.HasValue);
            Assert.Equal(cloudProxy1.Value, returnedValue.OrDefault());

            Try<ICloudProxy> cloudProxy2 = await connectionManager.GetOrCreateCloudConnectionAsync(deviceIdentityMock.Object);
            Assert.True(cloudProxy2.Success);
            Assert.True(cloudProxy2.Value.IsActive);
            Assert.Equal(cloudProxy1.Value, cloudProxy2.Value);

            Try<ICloudProxy> cloudProxy3 = await connectionManager.CreateCloudConnectionAsync(deviceIdentityMock.Object);
            Assert.True(cloudProxy3.Success);
            Assert.NotEqual(cloudProxy2.Value, cloudProxy3.Value);
            Assert.True(cloudProxy3.Value.IsActive);
            Assert.False(cloudProxy2.Value.IsActive);

            bool result = await connectionManager.CloseConnectionAsync(deviceIdentityMock.Object.Id);
            Assert.True(result);

            returnedValue = connectionManager.GetCloudConnection(deviceIdentityMock.Object.Id);
            Assert.False(returnedValue.HasValue);
        }

        [Fact]
        [Integration]
        public async Task MutipleModulesConnectionTest()
        {
            string iotHubHostName = "iotHubName";
            string edgeDeviceId = "edge";
            string edgeDeviceConnStr = "dummyConnStr";
            var module1Identity = new ModuleIdentity(iotHubHostName, edgeDeviceId, "module1", edgeDeviceConnStr, AuthenticationScope.SasToken, null, "xyz");

            var module2Identity = new ModuleIdentity(iotHubHostName, edgeDeviceId, "module2", edgeDeviceConnStr, AuthenticationScope.SasToken, null, "xyz"); 

            var edgeDeviceIdentity = Mock.Of<IDeviceIdentity>(
                d => d.DeviceId == edgeDeviceId &&
                    d.Id == edgeDeviceId &&
                    d.ConnectionString == edgeDeviceConnStr
            );

            var device1Identity = Mock.Of<IDeviceIdentity>(
                d => d.DeviceId == "device1" &&
                    d.Id == "device1" &&
                    d.ConnectionString == "Device1ConnStr"
            );

            Try<ICloudProxy> GetCloudProxy() => Try.Success(Mock.Of<ICloudProxy>(c => c.IsActive));
            var cloudProxyProvider = Mock.Of<ICloudProxyProvider>();
            Mock.Get(cloudProxyProvider).Setup(c => c.Connect(It.IsAny<IIdentity>())).ReturnsAsync(() => GetCloudProxy());

            var connectionManager = new ConnectionManager(cloudProxyProvider, EdgeDeviceId);
            Try<ICloudProxy> module1CloudProxy = await connectionManager.GetOrCreateCloudConnectionAsync(module1Identity);
            Assert.True(module1CloudProxy.Success);
            ICloudProxy edgeCloudProxy = module1CloudProxy.Value;
            Assert.NotNull(edgeCloudProxy);

            Try<ICloudProxy> module2CloudProxy = await connectionManager.GetOrCreateCloudConnectionAsync(module2Identity);
            Assert.True(module2CloudProxy.Success);            
            Assert.Equal(edgeCloudProxy, module2CloudProxy.Value);

            Try<ICloudProxy> edgeDeviceCloudProxy = await connectionManager.GetOrCreateCloudConnectionAsync(edgeDeviceIdentity);
            Assert.True(edgeDeviceCloudProxy.Success);
            Assert.Equal(edgeCloudProxy, edgeDeviceCloudProxy.Value);

            Try<ICloudProxy> device1CloudProxy = await connectionManager.GetOrCreateCloudConnectionAsync(device1Identity);
            Assert.True(device1CloudProxy.Success);
            Assert.NotEqual(edgeCloudProxy, device1CloudProxy.Value);
        }

        /// <summary>
        /// Tests that a device can connect and disconnect properly.
        /// 0. A cloud connection is established.
        /// 1. Device connects - a connection is added in the connection manager
        /// 2. Connection should have both cloud and device connections
        /// 3. Device disconnects - the device connection is removed. Cloud connection stays.
        /// 4. Connection manager should have a cloud connection, but no device connection.
        /// </summary>
        [Fact]
        [Integration]
        public async Task TestAddRemoveDeviceConnectionTest()
        {
            string deviceId = "id1";

            var deviceProxyMock1 = new Mock<IDeviceProxy>();
            deviceProxyMock1.SetupGet(dp => dp.IsActive).Returns(true);
            deviceProxyMock1.Setup(dp => dp.SetInactive()).Callback(() => deviceProxyMock1.SetupGet(dp => dp.IsActive).Returns(false));

            var identity = new Mock<IIdentity>();
            identity.SetupGet(i => i.Id).Returns(deviceId);

            var edgeHub = new Mock<IEdgeHub>();

            var cloudProxyMock = new Mock<ICloudProxy>();
            cloudProxyMock.Setup(c => c.BindCloudListener(It.IsAny<ICloudListener>()));
            cloudProxyMock.SetupGet(c => c.IsActive).Returns(true);
            cloudProxyMock.Setup(c => c.CloseAsync()).Callback(() => cloudProxyMock.SetupGet(c => c.IsActive).Returns(false));

            var cloudProviderMock = new Mock<ICloudProxyProvider>();
            cloudProviderMock.Setup(c => c.Connect(It.IsAny<IIdentity>())).ReturnsAsync(() => Try.Success(cloudProxyMock.Object));

            IConnectionManager connectionManager = new ConnectionManager(cloudProviderMock.Object, EdgeDeviceId);

            var deviceListener = new DeviceListener(identity.Object, edgeHub.Object, connectionManager, cloudProxyMock.Object);

            await connectionManager.CreateCloudConnectionAsync(identity.Object);
            Option<ICloudProxy> cloudProxy = connectionManager.GetCloudConnection(deviceId);
            Assert.True(cloudProxy.HasValue);
            Assert.Equal(cloudProxyMock.Object, cloudProxy.OrDefault());
            Assert.True(cloudProxyMock.Object.IsActive);

            deviceListener.BindDeviceProxy(deviceProxyMock1.Object);

            Option<IDeviceProxy> deviceProxy = connectionManager.GetDeviceConnection(deviceId);
            Assert.True(deviceProxy.HasValue);
            Assert.Equal(deviceProxyMock1.Object, deviceProxy.OrDefault());
            Assert.True(deviceProxyMock1.Object.IsActive);

            cloudProxy = connectionManager.GetCloudConnection(deviceId);
            Assert.True(cloudProxy.HasValue);
            Assert.Equal(cloudProxyMock.Object, cloudProxy.OrDefault());
            Assert.True(cloudProxyMock.Object.IsActive);

            await deviceListener.CloseAsync();

            deviceProxy = connectionManager.GetDeviceConnection(deviceId);
            Assert.False(deviceProxy.HasValue);
            Assert.False(deviceProxyMock1.Object.IsActive);

            cloudProxy = connectionManager.GetCloudConnection(deviceId);
            Assert.False(cloudProxy.HasValue);
            Assert.False(cloudProxyMock.Object.IsActive);
        }

        [Theory]
        [InlineData("device1/foo")]
        [InlineData("device1/")]
        [InlineData("device1")]
        public void IsEdgeDeviceTest_PositiveCase(string edgeDeviceId)
        {
            var cloudProviderMock = Mock.Of<ICloudProxyProvider>();
            ConnectionManager connectionManager = new ConnectionManager(cloudProviderMock, EdgeDeviceId);
            Assert.True(connectionManager.IsEdgeDevice(edgeDeviceId));
        }

        [Theory]
        [InlineData("foo")]
        [InlineData("device2/bar")]
        [InlineData("/device1")]
        [InlineData("")]
        public void IsEdgeDeviceTest_NegativeCase(string edgeDeviceId)
        {
            var cloudProviderMock = Mock.Of<ICloudProxyProvider>();
            ConnectionManager connectionManager = new ConnectionManager(cloudProviderMock, EdgeDeviceId);
            Assert.False(connectionManager.IsEdgeDevice(edgeDeviceId));
        }
    }
}