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
        const string DummyProductInfo = "foo";
        const string DummyToken = "abc";

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
            var deviceCredentials1 = Mock.Of<IClientCredentials>(c => c.Identity == deviceIdentityMock.Object);
            var deviceCredentials2 = Mock.Of<IClientCredentials>(c => c.Identity == deviceIdentityMock.Object);

            Option<IDeviceProxy> returnedDeviceProxy = connectionManager.GetDeviceConnection(deviceIdentityMock.Object.Id);
            Assert.False(returnedDeviceProxy.HasValue);

            await connectionManager.AddDeviceConnection(deviceCredentials1);
            connectionManager.BindDeviceProxy(deviceIdentityMock.Object, deviceProxyMock1.Object);
            Assert.True(deviceProxyMock1.Object.IsActive);

            returnedDeviceProxy = connectionManager.GetDeviceConnection(deviceIdentityMock.Object.Id);
            Assert.True(returnedDeviceProxy.HasValue);
            Assert.Equal(deviceProxyMock1.Object, returnedDeviceProxy.OrDefault());

            await connectionManager.AddDeviceConnection(deviceCredentials2);
            connectionManager.BindDeviceProxy(deviceIdentityMock.Object, deviceProxyMock2.Object);
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
            cloudProviderMock.Setup(p => p.Connect(It.IsAny<IClientCredentials>(), It.IsAny<Action<string, CloudConnectionStatus>>())).ReturnsAsync(() => Try.Success(cloudConnectionMock));

            // ReSharper disable once PossibleUnintendedReferenceComparison
            var deviceCredentials = Mock.Of<IClientCredentials>(c => c.Identity == Mock.Of<IIdentity>(d => d.Id == "Device1"));

            IConnectionManager connectionManager = new ConnectionManager(cloudProviderMock.Object);

            Option<ICloudProxy> returnedValue = await connectionManager.GetCloudConnection(deviceCredentials.Identity.Id);
            Assert.False(returnedValue.HasValue);

            Try<ICloudProxy> cloudProxy1 = await connectionManager.CreateCloudConnectionAsync(deviceCredentials);
            Assert.True(cloudProxy1.Success);
            Assert.True(cloudProxy1.Value.IsActive);

            returnedValue = await connectionManager.GetCloudConnection(deviceCredentials.Identity.Id);
            Assert.True(returnedValue.HasValue);
            Assert.Equal(cloudProxy1.Value, returnedValue.OrDefault());

            Try<ICloudProxy> cloudProxy2 = await connectionManager.CreateCloudConnectionAsync(deviceCredentials);
            Assert.True(cloudProxy2.Success);
            Assert.True(cloudProxy2.Value.IsActive);

            await connectionManager.RemoveDeviceConnection(deviceCredentials.Identity.Id);

            returnedValue = await connectionManager.GetCloudConnection(deviceCredentials.Identity.Id);
            Assert.False(returnedValue.HasValue);
        }

        [Fact]
        [Integration]
        public async Task MutipleModulesConnectionTest()
        {
            string iotHubHostName = "iotHubName";
            string edgeDeviceId = "edge";
            string edgeDeviceConnStr = "dummyConnStr";
            var module1Credentials = new TokenCredentials(new ModuleIdentity(iotHubHostName, edgeDeviceId, "module1"), "xyz", DummyProductInfo);
            var module2Credentials = new TokenCredentials(new ModuleIdentity(iotHubHostName, edgeDeviceId, "module2"), "xyz", DummyProductInfo);
            var edgeDeviceCredentials = new SharedKeyCredentials(new DeviceIdentity(iotHubHostName, edgeDeviceId), edgeDeviceConnStr, "abc");
            var device1Credentials = new TokenCredentials(new DeviceIdentity(iotHubHostName, edgeDeviceId), "pqr", DummyProductInfo);

            var cloudConnectionProvider = Mock.Of<ICloudConnectionProvider>();
            Mock.Get(cloudConnectionProvider)
                .Setup(c => c.Connect(It.IsAny<IClientCredentials>(), It.IsAny<Action<string, CloudConnectionStatus>>()))
                .ReturnsAsync(() => Try.Success(GetCloudConnectionMock()));

            var connectionManager = new ConnectionManager(cloudConnectionProvider);
            Try<ICloudProxy> module1CloudProxy = await connectionManager.CreateCloudConnectionAsync(module1Credentials);
            Assert.True(module1CloudProxy.Success);
            Assert.NotNull(module1CloudProxy.Value);

            Try<ICloudProxy> module2CloudProxy = await connectionManager.CreateCloudConnectionAsync(module2Credentials);
            Assert.True(module2CloudProxy.Success);
            Assert.NotEqual(module1CloudProxy.Value, module2CloudProxy.Value);

            Try<ICloudProxy> edgeDeviceCloudProxy = await connectionManager.CreateCloudConnectionAsync(edgeDeviceCredentials);
            Assert.True(edgeDeviceCloudProxy.Success);
            Assert.NotEqual(module1CloudProxy.Value, edgeDeviceCloudProxy.Value);

            Try<ICloudProxy> device1CloudProxy = await connectionManager.CreateCloudConnectionAsync(device1Credentials);
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

            var deviceCredentials = new SharedKeyCredentials(new DeviceIdentity("iotHub", deviceId), "dummyConnStr", "abc");

            var edgeHub = new Mock<IEdgeHub>();

            IClient client = GetDeviceClient();
            var messageConverterProvider = Mock.Of<IMessageConverterProvider>();
            var deviceClientProvider = new Mock<IClientProvider>();
            deviceClientProvider.Setup(d => d.Create(It.IsAny<IIdentity>(), It.IsAny<string>(), It.IsAny<Client.ITransportSettings[]>()))
                .Returns(client);

            var cloudConnectionProvider = new CloudConnectionProvider(messageConverterProvider, 1, deviceClientProvider.Object, Option.None<UpstreamProtocol>());
            IConnectionManager connectionManager = new ConnectionManager(cloudConnectionProvider);
            Try<ICloudProxy> cloudProxyTry = await connectionManager.CreateCloudConnectionAsync(deviceCredentials);
            Assert.True(cloudProxyTry.Success);
            var deviceListener = new DeviceMessageHandler(deviceCredentials.Identity, edgeHub.Object, connectionManager);

            Option<ICloudProxy> cloudProxy = await connectionManager.GetCloudConnection(deviceId);
            Assert.True(cloudProxy.HasValue);
            Assert.True(cloudProxy.OrDefault().IsActive);

            await connectionManager.AddDeviceConnection(deviceCredentials);
            deviceListener.BindDeviceProxy(deviceProxyMock1.Object);

            Option<IDeviceProxy> deviceProxy = connectionManager.GetDeviceConnection(deviceId);
            Assert.True(deviceProxy.HasValue);
            Assert.True(deviceProxy.OrDefault().IsActive);
            Assert.True(deviceProxyMock1.Object.IsActive);

            cloudProxy = await connectionManager.GetCloudConnection(deviceId);
            Assert.True(cloudProxy.HasValue);
            Assert.True(cloudProxy.OrDefault().IsActive);

            await deviceListener.CloseAsync();

            deviceProxy = connectionManager.GetDeviceConnection(deviceId);
            Assert.False(deviceProxy.HasValue);
            Assert.False(deviceProxyMock1.Object.IsActive);

            cloudProxy = await connectionManager.GetCloudConnection(deviceId);
            Assert.False(cloudProxy.HasValue);
            Assert.False(client.IsActive);
        }

        [Fact]
        [Unit]
        public async Task GetOrCreateCloudProxyTest()
        {
            string edgeDeviceId = "edgeDevice";
            string module1Id = "module1";
            string module2Id = "module2";
            string iotHubHostName = "iotHub";

            var module1Credentials = new TokenCredentials(new ModuleIdentity(iotHubHostName, edgeDeviceId, module1Id), DummyToken, DummyProductInfo);
            var module2Credentials = new TokenCredentials(new ModuleIdentity(iotHubHostName, edgeDeviceId, module2Id), DummyToken, DummyProductInfo);

            var cloudProxyMock1 = Mock.Of<ICloudProxy>();
            var cloudConnectionMock1 = Mock.Of<ICloudConnection>(cp => cp.IsActive && cp.CloudProxy == Option.Some(cloudProxyMock1));
            var cloudProxyMock2 = Mock.Of<ICloudProxy>();
            var cloudConnectionMock2 = Mock.Of<ICloudConnection>(cp => cp.IsActive && cp.CloudProxy == Option.Some(cloudProxyMock2));
            var cloudProxyProviderMock = new Mock<ICloudConnectionProvider>();
            cloudProxyProviderMock.Setup(c => c.Connect(It.Is<IClientCredentials>(i => i.Identity.Id == "edgeDevice/module1"), It.IsAny<Action<string, CloudConnectionStatus>>()))
                .ReturnsAsync(() => Try.Success(cloudConnectionMock1));
            cloudProxyProviderMock.Setup(c => c.Connect(It.Is<IClientCredentials>(i => i.Identity.Id == "edgeDevice/module2"), It.IsAny<Action<string, CloudConnectionStatus>>()))
                .ReturnsAsync(() => Try.Success(cloudConnectionMock2));

            var connectionManager = new ConnectionManager(cloudProxyProviderMock.Object);

            Task<Try<ICloudProxy>> getCloudProxyTask1 = connectionManager.GetOrCreateCloudConnectionAsync(module1Credentials);
            Task<Try<ICloudProxy>> getCloudProxyTask2 = connectionManager.GetOrCreateCloudConnectionAsync(module2Credentials);
            Task<Try<ICloudProxy>> getCloudProxyTask3 = connectionManager.GetOrCreateCloudConnectionAsync(module1Credentials);
            Try<ICloudProxy> cloudProxy1 = await getCloudProxyTask1;
            Try<ICloudProxy> cloudProxy2 = await getCloudProxyTask2;
            Try<ICloudProxy> cloudProxy3 = await getCloudProxyTask3;

            Assert.True(cloudProxy1.Success);
            Assert.True(cloudProxy2.Success);
            Assert.True(cloudProxy3.Success);
            Assert.Equal(cloudProxyMock1, cloudProxy1.Value);
            Assert.Equal(cloudProxyMock2, cloudProxy2.Value);
            Assert.Equal(cloudProxyMock1, cloudProxy3.Value);
            cloudProxyProviderMock.Verify(c => c.Connect(It.IsAny<IClientCredentials>(), It.IsAny<Action<string, CloudConnectionStatus>>()), Times.Exactly(2));
        }

        [Fact]
        [Unit]
        public async Task CreateCloudProxyTest()
        {
            string edgeDeviceId = "edgeDevice";
            string module1Id = "module1";

            var module1Credentials = new SharedKeyCredentials(new ModuleIdentity("iotHub", edgeDeviceId, module1Id), "connStr", DummyProductInfo);

            IClient client1 = GetDeviceClient();
            IClient client2 = GetDeviceClient();
            var messageConverterProvider = Mock.Of<IMessageConverterProvider>();
            var deviceClientProvider = new Mock<IClientProvider>();
            deviceClientProvider.SetupSequence(d => d.Create(It.IsAny<IIdentity>(), It.IsAny<string>(), It.IsAny<Client.ITransportSettings[]>()))
                .Returns(client1)
                .Returns(client2);

            var cloudConnectionProvider = new CloudConnectionProvider(messageConverterProvider, 1, deviceClientProvider.Object, Option.None<UpstreamProtocol>());
            IConnectionManager connectionManager = new ConnectionManager(cloudConnectionProvider);

            Task<Try<ICloudProxy>> getCloudProxyTask1 = connectionManager.CreateCloudConnectionAsync(module1Credentials);
            Task<Try<ICloudProxy>> getCloudProxyTask2 = connectionManager.CreateCloudConnectionAsync(module1Credentials);
            Try<ICloudProxy>[] cloudProxies = await Task.WhenAll(getCloudProxyTask1, getCloudProxyTask2);

            Assert.NotEqual(cloudProxies[0].Value, cloudProxies[1].Value);

            Option<ICloudProxy> currentCloudProxyId1 = await connectionManager.GetCloudConnection(module1Credentials.Identity.Id);
            ICloudProxy currentCloudProxy = currentCloudProxyId1.OrDefault();
            ICloudProxy cloudProxy1 = cloudProxies[0].Value;
            ICloudProxy cloudProxy2 = cloudProxies[1].Value;
            Assert.True(currentCloudProxy == cloudProxy1 || currentCloudProxy == cloudProxy2);
            if (currentCloudProxy == cloudProxy1)
            {
                Mock.Get(client2).Verify(cp => cp.CloseAsync(), Times.Once);
                Mock.Get(client1).Verify(cp => cp.CloseAsync(), Times.Never);
            }
            else
            {
                Mock.Get(client1).Verify(cp => cp.CloseAsync(), Times.Once);
                Mock.Get(client2).Verify(cp => cp.CloseAsync(), Times.Never);
            }
        }

        [Fact]
        [Unit]
        public async Task CloudProxyCallbackTest()
        {
            string device = "device1";
            var deviceCredentials = new TokenCredentials(new DeviceIdentity("iotHub", device), "dummyToken", DummyProductInfo);

            Action<string, CloudConnectionStatus> callback = null;
            var cloudProxy = Mock.Of<ICloudProxy>(c => c.IsActive);
            var cloudConnection = Mock.Of<ICloudConnection>(
                cp => cp.IsActive && cp.CloseAsync() == Task.FromResult(true) && cp.CloudProxy == Option.Some(cloudProxy));
            var cloudProxyProviderMock = new Mock<ICloudConnectionProvider>();
            cloudProxyProviderMock.Setup(c => c.Connect(It.IsAny<IClientCredentials>(), It.IsAny<Action<string, CloudConnectionStatus>>()))
                .Callback<IClientCredentials, Action<string, CloudConnectionStatus>>((i, c) => callback = c)
                .ReturnsAsync(Try.Success(cloudConnection));

            var deviceProxy = new Mock<IDeviceProxy>(MockBehavior.Strict);
            deviceProxy.Setup(d => d.GetUpdatedIdentity()).ReturnsAsync(Option.None<IClientCredentials>());
            deviceProxy.Setup(d => d.CloseAsync(It.Is<Exception>(e => e is EdgeHubConnectionException))).Returns(Task.CompletedTask);
            deviceProxy.SetupGet(d => d.IsActive).Returns(true);

            var connectionManager = new ConnectionManager(cloudProxyProviderMock.Object);
            await connectionManager.AddDeviceConnection(deviceCredentials);
            connectionManager.BindDeviceProxy(deviceCredentials.Identity, deviceProxy.Object);
            Try<ICloudProxy> cloudProxyTry = await connectionManager.GetOrCreateCloudConnectionAsync(deviceCredentials);

            Assert.True(cloudProxyTry.Success);
            Assert.NotNull(callback);

            callback.Invoke(device, CloudConnectionStatus.TokenNearExpiry);
            Mock.VerifyAll(deviceProxy);
        }

        [Fact]
        [Unit]
        public async Task CloudProxyCallbackTest2()
        {
            string device = "device1";
            var deviceIdentity = new DeviceIdentity("iotHub", device);
            IClientCredentials deviceCredentials = new TokenCredentials(deviceIdentity, "dummyToken", DummyProductInfo);
            IClientCredentials updatedDeviceCredentials = new TokenCredentials(deviceIdentity, "dummyToken", DummyProductInfo);

            Action<string, CloudConnectionStatus> callback = null;
            var cloudProxy = Mock.Of<ICloudProxy>(c => c.IsActive);
            var cloudConnection = Mock.Of<ICloudConnection>(
                cp => cp.IsActive && cp.CloudProxy == Option.Some(cloudProxy));
            bool updatedCredentialsPassed = false;
            Mock.Get(cloudConnection).Setup(c => c.CreateOrUpdateAsync(updatedDeviceCredentials))
                .Callback(() => updatedCredentialsPassed = true)
                .ReturnsAsync(cloudProxy);
            var cloudProxyProviderMock = new Mock<ICloudConnectionProvider>();
            cloudProxyProviderMock.Setup(c => c.Connect(It.IsAny<IClientCredentials>(), It.IsAny<Action<string, CloudConnectionStatus>>()))
                .Callback<IClientCredentials, Action<string, CloudConnectionStatus>>((i, c) => callback = c)
                .ReturnsAsync(Try.Success(cloudConnection));

            var deviceProxy = new Mock<IDeviceProxy>(MockBehavior.Strict);
            deviceProxy.Setup(d => d.GetUpdatedIdentity()).ReturnsAsync(Option.Some(updatedDeviceCredentials));
            deviceProxy.SetupGet(d => d.IsActive).Returns(true);

            var connectionManager = new ConnectionManager(cloudProxyProviderMock.Object);
            await connectionManager.AddDeviceConnection(deviceCredentials);
            connectionManager.BindDeviceProxy(deviceCredentials.Identity, deviceProxy.Object);
            Try<ICloudProxy> cloudProxyTry = await connectionManager.GetOrCreateCloudConnectionAsync(deviceCredentials);

            Assert.True(cloudProxyTry.Success);
            Assert.NotNull(callback);

            callback.Invoke(device, CloudConnectionStatus.TokenNearExpiry);
            Mock.VerifyAll(deviceProxy);
            Assert.True(updatedCredentialsPassed);
        }

        [Fact]
        [Unit]
        public async Task CloudConnectionUpdateTest()
        {
            string receivedConnStr = null;
            var messageConverterProvider = Mock.Of<IMessageConverterProvider>();
            var deviceClientProvider = new Mock<IClientProvider>();
            deviceClientProvider.Setup(d => d.Create(It.IsAny<IIdentity>(), It.IsAny<string>(), It.IsAny<Client.ITransportSettings[]>()))
                .Callback<IIdentity, string, Client.ITransportSettings[]>((i, s, t) => receivedConnStr = s)
                .Returns(() => GetDeviceClient());

            var cloudConnectionProvider = new CloudConnectionProvider(messageConverterProvider, 1, deviceClientProvider.Object, Option.None<UpstreamProtocol>());
            IConnectionManager connectionManager = new ConnectionManager(cloudConnectionProvider);

            string deviceConnStr1 = "connstr1";
            var deviceCredentials = new SharedKeyCredentials(new DeviceIdentity("iotHub", "Device1"), deviceConnStr1, DummyProductInfo);
            var deviceProxy = Mock.Of<IDeviceProxy>(d => d.IsActive);

            Try<ICloudProxy> receivedCloudProxy1 = await connectionManager.CreateCloudConnectionAsync(deviceCredentials);
            await connectionManager.AddDeviceConnection(deviceCredentials);
            connectionManager.BindDeviceProxy(deviceCredentials.Identity, deviceProxy);
            Assert.True(receivedCloudProxy1.Success);
            Assert.NotNull(receivedCloudProxy1.Value);
            Assert.True(receivedCloudProxy1.Value.IsActive);
            Assert.Equal(deviceConnStr1, receivedConnStr);
            Assert.Equal(deviceProxy, connectionManager.GetDeviceConnection(deviceCredentials.Identity.Id).OrDefault());

            string deviceConnStr2 = "connstr2";
            deviceCredentials = new SharedKeyCredentials(new DeviceIdentity("iotHub", "Device1"), deviceConnStr2, DummyProductInfo);

            Try<ICloudProxy> receivedCloudProxy2 = await connectionManager.CreateCloudConnectionAsync(deviceCredentials);
            Assert.True(receivedCloudProxy2.Success);
            Assert.NotNull(receivedCloudProxy2.Value);
            Assert.True(receivedCloudProxy2.Value.IsActive);
            Assert.False(receivedCloudProxy1.Value.IsActive);
            Assert.Equal(deviceConnStr2, receivedConnStr);
            Assert.Equal(deviceProxy, connectionManager.GetDeviceConnection(deviceCredentials.Identity.Id).OrDefault());
        }

        [Fact]
        [Unit]
        public async Task CloudConnectionInvalidUpdateTest()
        {
            var messageConverterProvider = Mock.Of<IMessageConverterProvider>();
            var deviceClientProvider = new Mock<IClientProvider>();
            deviceClientProvider.SetupSequence(d => d.Create(It.IsAny<IIdentity>(), It.IsAny<string>(), It.IsAny<Client.ITransportSettings[]>()))
                .Returns(GetDeviceClient())
                .Throws(new UnauthorizedException("connstr2 is invalid!"))
                .Throws(new UnauthorizedException("connstr2 is invalid!"));

            var cloudConnectionProvider = new CloudConnectionProvider(messageConverterProvider, 1, deviceClientProvider.Object, Option.None<UpstreamProtocol>());
            IConnectionManager connectionManager = new ConnectionManager(cloudConnectionProvider);

            string deviceConnStr1 = "connstr1";
            var deviceCredentials = new SharedKeyCredentials(new DeviceIdentity("iotHub", "Device1"), deviceConnStr1, DummyProductInfo);
            var deviceProxy = Mock.Of<IDeviceProxy>(d => d.IsActive);

            Try<ICloudProxy> receivedCloudProxy1 = await connectionManager.CreateCloudConnectionAsync(deviceCredentials);
            await connectionManager.AddDeviceConnection(deviceCredentials);
            connectionManager.BindDeviceProxy(deviceCredentials.Identity, deviceProxy);
            Assert.True(receivedCloudProxy1.Success);
            Assert.NotNull(receivedCloudProxy1.Value);
            Assert.True(receivedCloudProxy1.Value.IsActive);
            Assert.Equal(deviceProxy, connectionManager.GetDeviceConnection(deviceCredentials.Identity.Id).OrDefault());

            string deviceConnStr2 = "connstr2";
            deviceCredentials = new SharedKeyCredentials(new DeviceIdentity("iotHub", "Device1"), deviceConnStr2, DummyProductInfo);

            Try<ICloudProxy> receivedCloudProxy2 = await connectionManager.CreateCloudConnectionAsync(deviceCredentials);
            Assert.False(receivedCloudProxy2.Success);
            Assert.IsType<EdgeHubConnectionException>(receivedCloudProxy2.Exception);
            List<Exception> innerExceptions = (receivedCloudProxy2.Exception.InnerException as AggregateException)?.InnerExceptions.ToList() ?? new List<Exception>();
            Assert.Equal(2, innerExceptions.Count);
            Assert.IsType<UnauthorizedException>(innerExceptions[0]);
            Assert.IsType<UnauthorizedException>(innerExceptions[1]);
            Assert.True(receivedCloudProxy1.Value.IsActive);
            Assert.Equal(deviceProxy, connectionManager.GetDeviceConnection(deviceCredentials.Identity.Id).OrDefault());
        }

        [Fact]
        [Unit]
        public async Task MaxClientsTest()
        {
            var cloudProviderMock = new Mock<ICloudConnectionProvider>();
            cloudProviderMock.Setup(p => p.Connect(It.IsAny<IClientCredentials>(), It.IsAny<Action<string, CloudConnectionStatus>>()))
                .ReturnsAsync(() => Try.Success(GetCloudConnectionMock()));

            var deviceIdentity1 = Mock.Of<IDeviceIdentity>(d => d.Id == "Device1");
            var deviceIdentity2 = Mock.Of<IDeviceIdentity>(d => d.Id == "Device2");
            var deviceIdentity3 = Mock.Of<IDeviceIdentity>(d => d.Id == "Device3");

            var deviceCredentials1 = Mock.Of<IClientCredentials>(c => c.Identity == deviceIdentity1);
            var deviceCredentials2 = Mock.Of<IClientCredentials>(c => c.Identity == deviceIdentity2);
            var deviceCredentials3 = Mock.Of<IClientCredentials>(c => c.Identity == deviceIdentity3);

            var connectionManager = new ConnectionManager(cloudProviderMock.Object, 2);

            await connectionManager.AddDeviceConnection(deviceCredentials1);
            await connectionManager.AddDeviceConnection(deviceCredentials2);
            await Assert.ThrowsAsync<EdgeHubConnectionException>(async () => await connectionManager.AddDeviceConnection(deviceCredentials3));
        }

        [Fact]
        [Unit]
        public async Task AddRemoveSubscriptionsTest()
        {
            // Arrange
            string deviceId = "d1";
            var cloudConnectionProvider = Mock.Of<ICloudConnectionProvider>();
            var connectionManager = new ConnectionManager(cloudConnectionProvider);
            var identity = Mock.Of<IIdentity>(i => i.Id == deviceId);
            // ReSharper disable once PossibleUnintendedReferenceComparison
            var deviceCredentials = Mock.Of<IClientCredentials>(c => c.Identity == identity);

            // Act
            await connectionManager.AddDeviceConnection(deviceCredentials);
            Option<IReadOnlyDictionary<DeviceSubscription, bool>> subscriptionsOption = connectionManager.GetSubscriptions(deviceId);

            // Assert
            Assert.True(subscriptionsOption.HasValue);
            IReadOnlyDictionary<DeviceSubscription, bool> subscriptions = subscriptionsOption.OrDefault();
            Assert.Empty(subscriptions);

            // Act
            connectionManager.AddSubscription(deviceId, DeviceSubscription.Methods);
            connectionManager.AddSubscription(deviceId, DeviceSubscription.C2D);
            subscriptionsOption = connectionManager.GetSubscriptions(deviceId);

            // Assert
            Assert.True(subscriptionsOption.HasValue);
            subscriptions = subscriptionsOption.OrDefault();
            Assert.Equal(2, subscriptions.Count);
            Assert.Equal(true, subscriptions[DeviceSubscription.Methods]);
            Assert.Equal(true, subscriptions[DeviceSubscription.C2D]);

            // Act
            connectionManager.RemoveSubscription(deviceId, DeviceSubscription.Methods);
            connectionManager.RemoveSubscription(deviceId, DeviceSubscription.DesiredPropertyUpdates);
            subscriptionsOption = connectionManager.GetSubscriptions(deviceId);

            // Assert
            Assert.True(subscriptionsOption.HasValue);
            subscriptions = subscriptionsOption.OrDefault();
            Assert.Equal(3, subscriptions.Count);
            Assert.Equal(false, subscriptions[DeviceSubscription.Methods]);
            Assert.Equal(true, subscriptions[DeviceSubscription.C2D]);
            Assert.Equal(false, subscriptions[DeviceSubscription.DesiredPropertyUpdates]);
        }

        [Fact]
        [Unit]
        public async Task KeepSubscriptionsOnDeviceRemoveTest()
        {
            // Arrange
            string deviceId = "d1";
            var cloudConnectionProvider = Mock.Of<ICloudConnectionProvider>();
            var connectionManager = new ConnectionManager(cloudConnectionProvider);
            var identity = Mock.Of<IIdentity>(i => i.Id == deviceId);
            var credentials1 = Mock.Of<IClientCredentials>(c => c.Identity == identity);
            var credentials2 = Mock.Of<IClientCredentials>(c => c.Identity == identity);
            bool isProxyActive = true;
            // ReSharper disable once PossibleUnintendedReferenceComparison
            var deviceProxy = Mock.Of<IDeviceProxy>(d => d.Identity == identity);
            Mock.Get(deviceProxy).Setup(d => d.CloseAsync(It.IsAny<Exception>()))
                .Callback(() => isProxyActive = false)
                .Returns(Task.CompletedTask);
            Mock.Get(deviceProxy).SetupGet(d => d.IsActive)
                .Returns(() => isProxyActive);
            // ReSharper disable once PossibleUnintendedReferenceComparison
            var deviceProxy2 = Mock.Of<IDeviceProxy>(d => d.IsActive && d.Identity == identity);

            // Act
            Option<IReadOnlyDictionary<DeviceSubscription, bool>> subscriptionsOption = connectionManager.GetSubscriptions(deviceId);

            // Assert
            Assert.False(subscriptionsOption.HasValue);

            // Act
            await connectionManager.AddDeviceConnection(credentials1);
            connectionManager.BindDeviceProxy(identity, deviceProxy);
            subscriptionsOption = connectionManager.GetSubscriptions(deviceId);

            // Assert
            Assert.True(subscriptionsOption.HasValue);
            IReadOnlyDictionary<DeviceSubscription, bool> subscriptions = subscriptionsOption.OrDefault();
            Assert.Empty(subscriptions);

            // Act
            connectionManager.AddSubscription(deviceId, DeviceSubscription.Methods);
            connectionManager.AddSubscription(deviceId, DeviceSubscription.C2D);
            subscriptionsOption = connectionManager.GetSubscriptions(deviceId);

            // Assert
            Assert.True(subscriptionsOption.HasValue);
            subscriptions = subscriptionsOption.OrDefault();
            Assert.Equal(2, subscriptions.Count);
            Assert.Equal(true, subscriptions[DeviceSubscription.Methods]);
            Assert.Equal(true, subscriptions[DeviceSubscription.C2D]);

            // Act
            await connectionManager.RemoveDeviceConnection(deviceId);
            subscriptionsOption = connectionManager.GetSubscriptions(deviceId);

            // Assert
            Assert.False(subscriptionsOption.HasValue);

            // Act
            await connectionManager.AddDeviceConnection(credentials2);
            connectionManager.BindDeviceProxy(identity, deviceProxy2);
            subscriptionsOption = connectionManager.GetSubscriptions(deviceId);

            // Assert
            Assert.True(subscriptionsOption.HasValue);
            subscriptions = subscriptionsOption.OrDefault();
            Assert.Equal(2, subscriptions.Count);
            Assert.Equal(true, subscriptions[DeviceSubscription.Methods]);
            Assert.Equal(true, subscriptions[DeviceSubscription.C2D]);

            // Act
            connectionManager.AddSubscription(deviceId, DeviceSubscription.DesiredPropertyUpdates);
            connectionManager.AddSubscription(deviceId, DeviceSubscription.ModuleMessages);
            subscriptionsOption = connectionManager.GetSubscriptions(deviceId);

            // Assert
            Assert.True(subscriptionsOption.HasValue);
            subscriptions = subscriptionsOption.OrDefault();
            Assert.Equal(4, subscriptions.Count);
            Assert.Equal(true, subscriptions[DeviceSubscription.Methods]);
            Assert.Equal(true, subscriptions[DeviceSubscription.C2D]);
            Assert.Equal(true, subscriptions[DeviceSubscription.DesiredPropertyUpdates]);
            Assert.Equal(true, subscriptions[DeviceSubscription.ModuleMessages]);
        }

        static ICloudConnection GetCloudConnectionMock()
        {
            ICloudProxy cloudProxyMock = GetCloudProxyMock();
            var cloudConnectionMock = new Mock<ICloudConnection>();
            cloudConnectionMock.SetupGet(dp => dp.IsActive).Returns(true);
            cloudConnectionMock.SetupGet(dp => dp.CloudProxy).Returns(Option.Some(cloudProxyMock));
            cloudConnectionMock.Setup(c => c.CreateOrUpdateAsync(It.IsAny<IClientCredentials>()))
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

        static IClient GetDeviceClient()
        {
            var deviceClient = new Mock<IClient>();
            deviceClient.SetupGet(d => d.IsActive).Returns(true);
            deviceClient.Setup(d => d.CloseAsync())
                .Callback(() => deviceClient.SetupGet(d => d.IsActive).Returns(false))
                .Returns(Task.CompletedTask);
            return deviceClient.Object;
        }
    }
}
