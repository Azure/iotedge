// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client.Exceptions;
    using Microsoft.Azure.Devices.Edge.Hub.CloudProxy;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
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
            var cloudProviderMock = new Mock<ICloudConnectionProvider>();
            IConnectionManager connectionManager = new ConnectionManager(cloudProviderMock.Object);

            var deviceProxyMock1 = new Mock<IDeviceProxy>();
            deviceProxyMock1.SetupGet(dp => dp.IsActive).Returns(true);
            deviceProxyMock1.Setup(dp => dp.CloseAsync(It.IsAny<Exception>()))
                .Callback(() => deviceProxyMock1.SetupGet(dp => dp.IsActive).Returns(false))
                .Returns(Task.CompletedTask);

            var deviceProxyMock2 = new Mock<IDeviceProxy>();
            deviceProxyMock2.SetupGet(dp => dp.IsActive).Returns(true);
            deviceProxyMock2.Setup(dp => dp.CloseAsync(It.IsAny<Exception>()))
                .Callback(() => deviceProxyMock2.SetupGet(dp => dp.IsActive).Returns(false))
                .Returns(Task.CompletedTask);

            var deviceIdentityMock = new Mock<IIdentity>();
            deviceIdentityMock.SetupGet(di => di.Id).Returns("Device1");

            Option<IDeviceProxy> returnedDeviceProxy = connectionManager.GetDeviceConnection(deviceIdentityMock.Object.Id);
            Assert.False(returnedDeviceProxy.HasValue);

            await connectionManager.AddDeviceConnection(deviceIdentityMock.Object, deviceProxyMock1.Object);
            Assert.True(deviceProxyMock1.Object.IsActive);

            returnedDeviceProxy = connectionManager.GetDeviceConnection(deviceIdentityMock.Object.Id);
            Assert.True(returnedDeviceProxy.HasValue);
            Assert.Equal(deviceProxyMock1.Object, returnedDeviceProxy.OrDefault());

            await connectionManager.AddDeviceConnection(deviceIdentityMock.Object, deviceProxyMock2.Object);
            Assert.True(deviceProxyMock2.Object.IsActive);
            Assert.False(deviceProxyMock1.Object.IsActive);

            returnedDeviceProxy = connectionManager.GetDeviceConnection(deviceIdentityMock.Object.Id);
            Assert.True(returnedDeviceProxy.HasValue);
            Assert.Equal(deviceProxyMock2.Object, returnedDeviceProxy.OrDefault());

            await connectionManager.RemoveDeviceConnection(deviceIdentityMock.Object.Id);

            returnedDeviceProxy = connectionManager.GetDeviceConnection(deviceIdentityMock.Object.Id);
            Assert.False(returnedDeviceProxy.HasValue);
        }

        [Fact]
        [Integration]
        public async Task CloudConnectionTest()
        {
            ICloudConnection cloudConnectionMock = GetCloudConnectionMock();
            var cloudProviderMock = new Mock<ICloudConnectionProvider>();
            cloudProviderMock.Setup(p => p.Connect(It.IsAny<IIdentity>(), It.IsAny<Action<string, CloudConnectionStatus>>())).ReturnsAsync(() => Try.Success(cloudConnectionMock));

            var deviceIdentityMock = new Mock<IIdentity>();
            deviceIdentityMock.SetupGet(di => di.Id).Returns("Device1");

            IConnectionManager connectionManager = new ConnectionManager(cloudProviderMock.Object);

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

            await connectionManager.RemoveDeviceConnection(deviceIdentityMock.Object.Id);

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
            var module1Identity = new ModuleIdentity(iotHubHostName, edgeDeviceId, "module1", edgeDeviceConnStr, AuthenticationScope.SasToken, null, "xyz", "", Option.None<string>());

            var module2Identity = new ModuleIdentity(iotHubHostName, edgeDeviceId, "module2", edgeDeviceConnStr, AuthenticationScope.SasToken, null, "xyz", "", Option.None<string>());

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

            var cloudConnectionProvider = Mock.Of<ICloudConnectionProvider>();
            Mock.Get(cloudConnectionProvider)
                .Setup(c => c.Connect(It.IsAny<IIdentity>(), It.IsAny<Action<string, CloudConnectionStatus>>()))
                .ReturnsAsync(() => Try.Success(GetCloudConnectionMock()));

            var connectionManager = new ConnectionManager(cloudConnectionProvider);
            Try<ICloudProxy> module1CloudProxy = await connectionManager.GetOrCreateCloudConnectionAsync(module1Identity);
            Assert.True(module1CloudProxy.Success);
            Assert.NotNull(module1CloudProxy.Value);

            Try<ICloudProxy> module2CloudProxy = await connectionManager.GetOrCreateCloudConnectionAsync(module2Identity);
            Assert.True(module2CloudProxy.Success);
            Assert.NotEqual(module1CloudProxy.Value, module2CloudProxy.Value);

            Try<ICloudProxy> edgeDeviceCloudProxy = await connectionManager.GetOrCreateCloudConnectionAsync(edgeDeviceIdentity);
            Assert.True(edgeDeviceCloudProxy.Success);
            Assert.NotEqual(module1CloudProxy.Value, edgeDeviceCloudProxy.Value);

            Try<ICloudProxy> device1CloudProxy = await connectionManager.GetOrCreateCloudConnectionAsync(device1Identity);
            Assert.True(device1CloudProxy.Success);
            Assert.NotEqual(edgeDeviceCloudProxy.Value, device1CloudProxy.Value);
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
            deviceProxyMock1.Setup(dp => dp.CloseAsync(It.IsAny<Exception>()))
                .Callback(() => deviceProxyMock1.SetupGet(dp => dp.IsActive).Returns(false))
                .Returns(Task.FromResult(true));

            var identity = Mock.Of<IIdentity>(i => i.Id == deviceId && i.ConnectionString == "dummyConnStr");

            var edgeHub = new Mock<IEdgeHub>();

            IDeviceClient deviceClient = GetDeviceClient();
            var messageConverterProvider = Mock.Of<IMessageConverterProvider>();
            var deviceClientProvider = new Mock<IDeviceClientProvider>();
            deviceClientProvider.Setup(d => d.Create(It.IsAny<string>(), It.IsAny<Client.ITransportSettings[]>()))
                .Returns(deviceClient);

            var cloudConnectionProvider = new CloudConnectionProvider(messageConverterProvider, 1, deviceClientProvider.Object, Option.None<UpstreamProtocol>());
            IConnectionManager connectionManager = new ConnectionManager(cloudConnectionProvider);
            Try<ICloudProxy> cloudProxyTry = await connectionManager.CreateCloudConnectionAsync(identity);
            Assert.True(cloudProxyTry.Success);
            var deviceListener = new DeviceMessageHandler(identity, edgeHub.Object, connectionManager, cloudProxyTry.Value);

            Option<ICloudProxy> cloudProxy = connectionManager.GetCloudConnection(deviceId);
            Assert.True(cloudProxy.HasValue);
            Assert.True(cloudProxy.OrDefault().IsActive);

            deviceListener.BindDeviceProxy(deviceProxyMock1.Object);

            Option<IDeviceProxy> deviceProxy = connectionManager.GetDeviceConnection(deviceId);
            Assert.True(deviceProxy.HasValue);
            Assert.True(deviceProxy.OrDefault().IsActive);
            Assert.True(deviceProxyMock1.Object.IsActive);

            cloudProxy = connectionManager.GetCloudConnection(deviceId);
            Assert.True(cloudProxy.HasValue);
            Assert.True(cloudProxy.OrDefault().IsActive);

            await deviceListener.CloseAsync();

            deviceProxy = connectionManager.GetDeviceConnection(deviceId);
            Assert.False(deviceProxy.HasValue);
            Assert.False(deviceProxyMock1.Object.IsActive);

            cloudProxy = connectionManager.GetCloudConnection(deviceId);
            Assert.False(cloudProxy.HasValue);
            Assert.False(deviceClient.IsActive);
        }

        [Fact]
        [Unit]
        public async Task GetOrCreateCloudProxyTest()
        {
            string edgeDeviceId = "edgeDevice";
            string module1Id = "module1";
            string module2Id = "module2";

            var module1Identity = new ModuleIdentity(string.Empty,
                edgeDeviceId,
                module1Id,
                string.Empty,
                AuthenticationScope.SasToken,
                string.Empty,
                string.Empty,
                string.Empty,
                Option.None<string>());

            var module2Identity = new ModuleIdentity(string.Empty,
                edgeDeviceId,
                module2Id,
                string.Empty,
                AuthenticationScope.SasToken,
                string.Empty,
                string.Empty,
                string.Empty,
                Option.None<string>());

            var cloudProxyMock1 = Mock.Of<ICloudProxy>();
            var cloudConnectionMock1 = Mock.Of<ICloudConnection>(cp => cp.IsActive && cp.CloudProxy == Option.Some(cloudProxyMock1));
            var cloudProxyMock2 = Mock.Of<ICloudProxy>();
            var cloudConnectionMock2 = Mock.Of<ICloudConnection>(cp => cp.IsActive && cp.CloudProxy == Option.Some(cloudProxyMock2));
            var cloudProxyProviderMock = new Mock<ICloudConnectionProvider>();
            cloudProxyProviderMock.Setup(c => c.Connect(It.Is<IIdentity>(i => i.Id == "edgeDevice/module1"), It.IsAny<Action<string, CloudConnectionStatus>>()))
                .ReturnsAsync(() => Try.Success(cloudConnectionMock1));
            cloudProxyProviderMock.Setup(c => c.Connect(It.Is<IIdentity>(i => i.Id == "edgeDevice/module2"), It.IsAny<Action<string, CloudConnectionStatus>>()))
                .ReturnsAsync(() => Try.Success(cloudConnectionMock2));

            var connectionManager = new ConnectionManager(cloudProxyProviderMock.Object);

            Task<Try<ICloudProxy>> getCloudProxyTask1 = connectionManager.GetOrCreateCloudConnectionAsync(module1Identity);
            Task<Try<ICloudProxy>> getCloudProxyTask2 = connectionManager.GetOrCreateCloudConnectionAsync(module2Identity);
            Task<Try<ICloudProxy>> getCloudProxyTask3 = connectionManager.GetOrCreateCloudConnectionAsync(module1Identity);
            Try<ICloudProxy> cloudProxy1 = await getCloudProxyTask1;
            Try<ICloudProxy> cloudProxy2 = await getCloudProxyTask2;
            Try<ICloudProxy> cloudProxy3 = await getCloudProxyTask3;

            Assert.True(cloudProxy1.Success);
            Assert.True(cloudProxy2.Success);
            Assert.True(cloudProxy3.Success);
            Assert.Equal(cloudProxyMock1, cloudProxy1.Value);
            Assert.Equal(cloudProxyMock2, cloudProxy2.Value);
            Assert.Equal(cloudProxyMock1, cloudProxy3.Value);
            cloudProxyProviderMock.Verify(c => c.Connect(It.IsAny<IIdentity>(), It.IsAny<Action<string, CloudConnectionStatus>>()), Times.Exactly(2));
        }

        [Fact]
        [Unit]
        public async Task CreateCloudProxyTest()
        {
            string edgeDeviceId = "edgeDevice";
            string module1Id = "module1";

            var module1Identity = new ModuleIdentity(string.Empty,
                edgeDeviceId,
                module1Id,
                "dummyConnStr",
                AuthenticationScope.SasToken,
                string.Empty,
                string.Empty,
                string.Empty,
                Option.None<string>());

            IDeviceClient deviceClient1 = GetDeviceClient();
            IDeviceClient deviceClient2 = GetDeviceClient();
            var messageConverterProvider = Mock.Of<IMessageConverterProvider>();
            var deviceClientProvider = new Mock<IDeviceClientProvider>();
            deviceClientProvider.SetupSequence(d => d.Create(It.IsAny<string>(), It.IsAny<Client.ITransportSettings[]>()))
                .Returns(deviceClient1)
                .Returns(deviceClient2);

            var cloudConnectionProvider = new CloudConnectionProvider(messageConverterProvider, 1, deviceClientProvider.Object, Option.None<UpstreamProtocol>());
            IConnectionManager connectionManager = new ConnectionManager(cloudConnectionProvider);

            Task<Try<ICloudProxy>> getCloudProxyTask1 = connectionManager.CreateCloudConnectionAsync(module1Identity);
            Task<Try<ICloudProxy>> getCloudProxyTask2 = connectionManager.CreateCloudConnectionAsync(module1Identity);
            Try<ICloudProxy>[] cloudProxies = await Task.WhenAll(getCloudProxyTask1, getCloudProxyTask2);

            Assert.NotEqual(cloudProxies[0].Value, cloudProxies[1].Value);

            Option<ICloudProxy> currentCloudProxyId1 = connectionManager.GetCloudConnection(module1Identity.Id);
            ICloudProxy currentCloudProxy = currentCloudProxyId1.OrDefault();
            ICloudProxy cloudProxy1 = cloudProxies[0].Value;
            ICloudProxy cloudProxy2 = cloudProxies[1].Value;
            Assert.True(currentCloudProxy == cloudProxy1 || currentCloudProxy == cloudProxy2);
            if (currentCloudProxy == cloudProxy1)
            {
                Mock.Get(deviceClient2).Verify(cp => cp.CloseAsync(), Times.Once);
                Mock.Get(deviceClient1).Verify(cp => cp.CloseAsync(), Times.Never);
            }
            else
            {
                Mock.Get(deviceClient1).Verify(cp => cp.CloseAsync(), Times.Once);
                Mock.Get(deviceClient2).Verify(cp => cp.CloseAsync(), Times.Never);
            }
        }

        [Fact]
        [Unit]
        public async Task CloudProxyCallbackTest()
        {
            string device = "device1";
            var deviceIdentity = new DeviceIdentity(string.Empty,
                device,
                string.Empty,
                AuthenticationScope.SasToken,
                string.Empty,
                string.Empty,
                string.Empty,
                Option.None<string>());

            Action<string, CloudConnectionStatus> callback = null;
            var cloudProxy = Mock.Of<ICloudProxy>(c => c.IsActive);
            var cloudConnection = Mock.Of<ICloudConnection>(
                cp => cp.IsActive && cp.CloseAsync() == Task.FromResult(true) && cp.CloudProxy == Option.Some(cloudProxy));
            var cloudProxyProviderMock = new Mock<ICloudConnectionProvider>();
            cloudProxyProviderMock.Setup(c => c.Connect(It.IsAny<IIdentity>(), It.IsAny<Action<string, CloudConnectionStatus>>()))
                .Callback<IIdentity, Action<string, CloudConnectionStatus>>((i, c) => callback = c)
                .ReturnsAsync(Try.Success(cloudConnection));

            var deviceProxy = new Mock<IDeviceProxy>(MockBehavior.Strict);
            deviceProxy.Setup(d => d.CloseAsync(It.Is<Exception>(e => e is EdgeHubConnectionException))).Returns(Task.CompletedTask);
            deviceProxy.SetupGet(d => d.IsActive).Returns(true);

            var connectionManager = new ConnectionManager(cloudProxyProviderMock.Object);
            await connectionManager.AddDeviceConnection(deviceIdentity, deviceProxy.Object);
            Try<ICloudProxy> cloudProxyTry = await connectionManager.GetOrCreateCloudConnectionAsync(deviceIdentity);

            Assert.True(cloudProxyTry.Success);
            Assert.NotNull(callback);

            callback.Invoke(device, CloudConnectionStatus.TokenNearExpiry);
            Mock.VerifyAll(deviceProxy);
        }

        [Fact]
        [Unit]
        public async Task CloudConnectionUpdateTest()
        {
            string receivedConnStr = null;
            var messageConverterProvider = Mock.Of<IMessageConverterProvider>();
            var deviceClientProvider = new Mock<IDeviceClientProvider>();
            deviceClientProvider.Setup(d => d.Create(It.IsAny<string>(), It.IsAny<Client.ITransportSettings[]>()))
                .Callback<string, Client.ITransportSettings[]>((s, t) => receivedConnStr = s)
                .Returns(() => GetDeviceClient());

            var cloudConnectionProvider = new CloudConnectionProvider(messageConverterProvider, 1, deviceClientProvider.Object, Option.None<UpstreamProtocol>());
            IConnectionManager connectionManager = new ConnectionManager(cloudConnectionProvider);

            string deviceConnStr1 = "connstr1";
            var deviceIdentity = Mock.Of<IDeviceIdentity>(d => d.Id == "Device1" && d.ConnectionString == deviceConnStr1);
            var deviceProxy = Mock.Of<IDeviceProxy>(d => d.IsActive);

            Try<ICloudProxy> receivedCloudProxy1 = await connectionManager.CreateCloudConnectionAsync(deviceIdentity);
            await connectionManager.AddDeviceConnection(deviceIdentity, deviceProxy);
            Assert.True(receivedCloudProxy1.Success);
            Assert.NotNull(receivedCloudProxy1.Value);
            Assert.True(receivedCloudProxy1.Value.IsActive);
            Assert.Equal(deviceConnStr1, receivedConnStr);
            Assert.Equal(deviceProxy, connectionManager.GetDeviceConnection(deviceIdentity.Id).OrDefault());

            string deviceConnStr2 = "connstr2";
            deviceIdentity = Mock.Of<IDeviceIdentity>(d => d.Id == "Device1" && d.ConnectionString == deviceConnStr2);

            Try<ICloudProxy> receivedCloudProxy2 = await connectionManager.CreateCloudConnectionAsync(deviceIdentity);
            Assert.True(receivedCloudProxy2.Success);
            Assert.NotNull(receivedCloudProxy2.Value);
            Assert.True(receivedCloudProxy2.Value.IsActive);
            Assert.False(receivedCloudProxy1.Value.IsActive);
            Assert.Equal(deviceConnStr2, receivedConnStr);
            Assert.Equal(deviceProxy, connectionManager.GetDeviceConnection(deviceIdentity.Id).OrDefault());
        }

        [Fact]
        [Unit]
        public async Task CloudConnectionInvalidUpdateTest()
        {
            var messageConverterProvider = Mock.Of<IMessageConverterProvider>();
            var deviceClientProvider = new Mock<IDeviceClientProvider>();
            deviceClientProvider.SetupSequence(d => d.Create(It.IsAny<string>(), It.IsAny<Client.ITransportSettings[]>()))
                .Returns(GetDeviceClient())
                .Throws(new UnauthorizedException("connstr2 is invalid!"))
                .Throws(new UnauthorizedException("connstr2 is invalid!"));

            var cloudConnectionProvider = new CloudConnectionProvider(messageConverterProvider, 1, deviceClientProvider.Object, Option.None<UpstreamProtocol>());
            IConnectionManager connectionManager = new ConnectionManager(cloudConnectionProvider);

            string deviceConnStr1 = "connstr1";
            var deviceIdentity = Mock.Of<IDeviceIdentity>(d => d.Id == "Device1" && d.ConnectionString == deviceConnStr1);
            var deviceProxy = Mock.Of<IDeviceProxy>(d => d.IsActive);

            Try<ICloudProxy> receivedCloudProxy1 = await connectionManager.CreateCloudConnectionAsync(deviceIdentity);
            await connectionManager.AddDeviceConnection(deviceIdentity, deviceProxy);
            Assert.True(receivedCloudProxy1.Success);
            Assert.NotNull(receivedCloudProxy1.Value);
            Assert.True(receivedCloudProxy1.Value.IsActive);
            Assert.Equal(deviceProxy, connectionManager.GetDeviceConnection(deviceIdentity.Id).OrDefault());

            string deviceConnStr2 = "connstr2";
            deviceIdentity = Mock.Of<IDeviceIdentity>(d => d.Id == "Device1" && d.ConnectionString == deviceConnStr2);

            Try<ICloudProxy> receivedCloudProxy2 = await connectionManager.CreateCloudConnectionAsync(deviceIdentity);
            Assert.False(receivedCloudProxy2.Success);
            Assert.IsType<EdgeHubConnectionException>(receivedCloudProxy2.Exception);
            List<Exception> innerExceptions = (receivedCloudProxy2.Exception.InnerException as AggregateException)?.InnerExceptions.ToList() ?? new List<Exception>();
            Assert.Equal(2, innerExceptions.Count);
            Assert.IsType<UnauthorizedException>(innerExceptions[0]);
            Assert.IsType<UnauthorizedException>(innerExceptions[1]);
            Assert.True(receivedCloudProxy1.Value.IsActive);
            Assert.Equal(deviceProxy, connectionManager.GetDeviceConnection(deviceIdentity.Id).OrDefault());
        }

        [Fact]
        [Unit]
        public async Task MaxClientsTest()
        {
            var cloudProviderMock = new Mock<ICloudConnectionProvider>();
            cloudProviderMock.Setup(p => p.Connect(It.IsAny<IIdentity>(), It.IsAny<Action<string, CloudConnectionStatus>>()))
                .ReturnsAsync(() => Try.Success(GetCloudConnectionMock()));

            var deviceIdentity1 = Mock.Of<IDeviceIdentity>(d => d.Id == "Device1" && d.ConnectionString == "connstr");
            var deviceIdentity2 = Mock.Of<IDeviceIdentity>(d => d.Id == "Device2" && d.ConnectionString == "connstr");
            var deviceIdentity3 = Mock.Of<IDeviceIdentity>(d => d.Id == "Device3" && d.ConnectionString == "connstr");

            var deviceProxy1 = Mock.Of<IDeviceProxy>(d => d.IsActive);
            var deviceProxy2 = Mock.Of<IDeviceProxy>(d => d.IsActive);
            var deviceProxy3 = Mock.Of<IDeviceProxy>(d => d.IsActive);

            var connectionManager = new ConnectionManager(cloudProviderMock.Object, 2);

            await connectionManager.AddDeviceConnection(deviceIdentity1, deviceProxy1);
            await connectionManager.AddDeviceConnection(deviceIdentity2, deviceProxy2);
            await Assert.ThrowsAsync<EdgeHubConnectionException>(async () => await connectionManager.AddDeviceConnection(deviceIdentity3, deviceProxy3));
        }

        static ICloudConnection GetCloudConnectionMock()
        {
            ICloudProxy cloudProxyMock = GetCloudProxyMock();
            var cloudConnectionMock = new Mock<ICloudConnection>();
            cloudConnectionMock.SetupGet(dp => dp.IsActive).Returns(true);
            cloudConnectionMock.SetupGet(dp => dp.CloudProxy).Returns(Option.Some(cloudProxyMock));
            cloudConnectionMock.Setup(c => c.CreateOrUpdateAsync(It.IsAny<IIdentity>()))
                .Callback(() =>
                {
                    cloudProxyMock = GetCloudProxyMock();
                    cloudConnectionMock.SetupGet(dp => dp.CloudProxy).Returns(Option.Some(cloudProxyMock));
                })
                .ReturnsAsync(cloudProxyMock);
            cloudConnectionMock.Setup(dp => dp.CloseAsync()).Returns(Task.FromResult(true))
                .Callback(() =>
                {
                    cloudConnectionMock.SetupGet(dp => dp.IsActive).Returns(false);
                    cloudConnectionMock.SetupGet(dp => dp.CloudProxy).Returns(Option.None<ICloudProxy>());
                });
            return cloudConnectionMock.Object;
        }

        static ICloudProxy GetCloudProxyMock()
        {
            var cloudProxyMock = new Mock<ICloudProxy>();
            cloudProxyMock.SetupGet(cp => cp.IsActive).Returns(true);
            return cloudProxyMock.Object;
        }

        static IDeviceClient GetDeviceClient()
        {
            var deviceClient = new Mock<IDeviceClient>();
            deviceClient.SetupGet(d => d.IsActive).Returns(true);
            deviceClient.Setup(d => d.CloseAsync())
                .Callback(() => deviceClient.SetupGet(d => d.IsActive).Returns(false))
                .Returns(Task.CompletedTask);
            return deviceClient.Object;
        }
    }
}
