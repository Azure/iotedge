// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
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
        const string EdgeDeviceId = "testEdgeDeviceId";
        const string EdgeModuleId = "$edgeHub";
        const string IotHubHostName = "foo.azure-devices.net";

        [Fact]
        [Integration]
        public async Task DeviceConnectionTest()
        {
            var cloudProviderMock = new Mock<ICloudConnectionProvider>();
            var credentialsManager = Mock.Of<ICredentialsCache>();
            IConnectionManager connectionManager = new ConnectionManager(cloudProviderMock.Object, credentialsManager, GetIdentityProvider());

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
            // ReSharper disable once PossibleUnintendedReferenceComparison
            var deviceCredentials1 = Mock.Of<ITokenCredentials>(c => c.Identity == Mock.Of<IIdentity>(d => d.Id == "Device1"));
            // ReSharper disable once PossibleUnintendedReferenceComparison
            var deviceCredentials2 = Mock.Of<ITokenCredentials>(c => c.Identity == Mock.Of<IIdentity>(d => d.Id == "Device2"));

            string edgeDeviceId = "edgeDevice";
            IClient client1 = GetDeviceClient();
            IClient client2 = GetDeviceClient();
            var messageConverterProvider = Mock.Of<IMessageConverterProvider>();
            var deviceClientProvider = new Mock<IClientProvider>();
            deviceClientProvider.SetupSequence(d => d.Create(It.IsAny<IIdentity>(), It.IsAny<ITokenProvider>(), It.IsAny<ITransportSettings[]>()))
                .Returns(client1)
                .Returns(client2);

            ICredentialsCache credentialsCache = new CredentialsCache(new NullCredentialsCache());

            var cloudConnectionProvider = new CloudConnectionProvider(
                messageConverterProvider,
                1,
                deviceClientProvider.Object,
                Option.None<UpstreamProtocol>(),
                Mock.Of<ITokenProvider>(),
                Mock.Of<IDeviceScopeIdentitiesCache>(),
                credentialsCache,
                new ModuleIdentity(IotHubHostName, edgeDeviceId, "$edgeHub"),
                TimeSpan.FromMinutes(60),
                true,
                TimeSpan.FromSeconds(20),
                Option.None<IWebProxy>());
            cloudConnectionProvider.BindEdgeHub(Mock.Of<IEdgeHub>());

            IConnectionManager connectionManager = new ConnectionManager(cloudConnectionProvider, credentialsCache, GetIdentityProvider());

            Option<ICloudProxy> returnedValue = await connectionManager.GetCloudConnection(deviceCredentials1.Identity.Id);
            Assert.False(returnedValue.HasValue);

            Try<ICloudProxy> cloudProxy1 = await connectionManager.CreateCloudConnectionAsync(deviceCredentials1);
            Assert.True(cloudProxy1.Success);
            Assert.True(cloudProxy1.Value.IsActive);

            returnedValue = await connectionManager.GetCloudConnection(deviceCredentials1.Identity.Id);
            Assert.True(returnedValue.HasValue);
            Assert.Equal(cloudProxy1.Value, returnedValue.OrDefault());

            Try<ICloudProxy> cloudProxy2 = await connectionManager.CreateCloudConnectionAsync(deviceCredentials2);
            Assert.True(cloudProxy2.Success);
            Assert.True(cloudProxy2.Value.IsActive);

            await connectionManager.RemoveDeviceConnection(deviceCredentials2.Identity.Id);

            returnedValue = await connectionManager.GetCloudConnection(deviceCredentials2.Identity.Id);
            Assert.True(returnedValue.HasValue);
        }

        [Fact]
        [Integration]
        public async Task MutipleModulesConnectionTest()
        {
            string iotHubHostName = "iotHubName";
            string edgeDeviceId = "edge";
            string edgeDeviceConnStr = "dummyConnStr";
            var module1Credentials = new TokenCredentials(new ModuleIdentity(iotHubHostName, edgeDeviceId, "module1"), "xyz", DummyProductInfo, false);
            var module2Credentials = new TokenCredentials(new ModuleIdentity(iotHubHostName, edgeDeviceId, "module2"), "xyz", DummyProductInfo, false);
            var edgeDeviceCredentials = new SharedKeyCredentials(new DeviceIdentity(iotHubHostName, edgeDeviceId), edgeDeviceConnStr, "abc");
            var device1Credentials = new TokenCredentials(new DeviceIdentity(iotHubHostName, edgeDeviceId), "pqr", DummyProductInfo, false);

            var cloudConnectionProvider = Mock.Of<ICloudConnectionProvider>();
            Mock.Get(cloudConnectionProvider)
                .Setup(c => c.Connect(It.IsAny<IClientCredentials>(), It.IsAny<Action<string, CloudConnectionStatus>>()))
                .ReturnsAsync(() => Try.Success(GetCloudConnectionMock()));

            var credentialsManager = Mock.Of<ICredentialsCache>();

            var connectionManager = new ConnectionManager(cloudConnectionProvider, credentialsManager, GetIdentityProvider());
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

            var deviceCredentials = new TokenCredentials(new DeviceIdentity("iotHub", deviceId), "token", "abc", false);

            var edgeHub = new Mock<IEdgeHub>();

            IClient client = GetDeviceClient();
            var messageConverterProvider = Mock.Of<IMessageConverterProvider>();
            var deviceClientProvider = new Mock<IClientProvider>();
            deviceClientProvider.Setup(d => d.Create(It.IsAny<IIdentity>(), It.IsAny<ITokenProvider>(), It.IsAny<ITransportSettings[]>()))
                .Returns(client);

            var credentialsManager = Mock.Of<ICredentialsCache>();
            var edgeHubIdentity = Mock.Of<IIdentity>(i => i.Id == "edgeDevice/$edgeHub");
            var cloudConnectionProvider = new CloudConnectionProvider(
                messageConverterProvider,
                1,
                deviceClientProvider.Object,
                Option.None<UpstreamProtocol>(),
                Mock.Of<ITokenProvider>(),
                Mock.Of<IDeviceScopeIdentitiesCache>(),
                credentialsManager,
                edgeHubIdentity,
                TimeSpan.FromMinutes(60),
                true,
                TimeSpan.FromSeconds(20),
                Option.None<IWebProxy>());

            cloudConnectionProvider.BindEdgeHub(edgeHub.Object);
            IConnectionManager connectionManager = new ConnectionManager(cloudConnectionProvider, credentialsManager, GetIdentityProvider());
            Try<ICloudProxy> cloudProxyTry = await connectionManager.CreateCloudConnectionAsync(deviceCredentials);
            Assert.True(cloudProxyTry.Success);
            var deviceListener = new DeviceMessageHandler(deviceCredentials.Identity, edgeHub.Object, connectionManager);

            Option<ICloudProxy> cloudProxy = await connectionManager.GetCloudConnection(deviceId);
            Assert.True(cloudProxy.HasValue);
            Assert.True(cloudProxy.OrDefault().IsActive);

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
            Assert.True(cloudProxy.HasValue);
            Assert.True(client.IsActive);
        }

        [Fact]
        [Unit]
        public async Task GetOrCreateCloudProxyTest()
        {
            string edgeDeviceId = "edgeDevice";
            string module1Id = "module1";
            string module2Id = "module2";
            string iotHubHostName = "iotHub";

            var module1Credentials = new TokenCredentials(new ModuleIdentity(iotHubHostName, edgeDeviceId, module1Id), DummyToken, DummyProductInfo, false);
            var module2Credentials = new TokenCredentials(new ModuleIdentity(iotHubHostName, edgeDeviceId, module2Id), DummyToken, DummyProductInfo, false);

            var cloudProxyMock1 = Mock.Of<ICloudProxy>();
            var cloudConnectionMock1 = Mock.Of<ICloudConnection>(cp => cp.IsActive && cp.CloudProxy == Option.Some(cloudProxyMock1));
            var cloudProxyMock2 = Mock.Of<ICloudProxy>();
            var cloudConnectionMock2 = Mock.Of<ICloudConnection>(cp => cp.IsActive && cp.CloudProxy == Option.Some(cloudProxyMock2));
            var cloudProxyProviderMock = new Mock<ICloudConnectionProvider>();
            cloudProxyProviderMock.Setup(c => c.Connect(It.Is<IClientCredentials>(i => i.Identity.Id == "edgeDevice/module1"), It.IsAny<Action<string, CloudConnectionStatus>>()))
                .ReturnsAsync(() => Try.Success(cloudConnectionMock1));
            cloudProxyProviderMock.Setup(c => c.Connect(It.Is<IClientCredentials>(i => i.Identity.Id == "edgeDevice/module2"), It.IsAny<Action<string, CloudConnectionStatus>>()))
                .ReturnsAsync(() => Try.Success(cloudConnectionMock2));

            var credentialsCache = Mock.Of<ICredentialsCache>();
            var connectionManager = new ConnectionManager(cloudProxyProviderMock.Object, credentialsCache, GetIdentityProvider());

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

            var module1Credentials = new TokenCredentials(new ModuleIdentity("iotHub", edgeDeviceId, module1Id), "token", DummyProductInfo, false);

            IClient client1 = GetDeviceClient();
            IClient client2 = GetDeviceClient();
            var messageConverterProvider = Mock.Of<IMessageConverterProvider>();
            var deviceClientProvider = new Mock<IClientProvider>();
            deviceClientProvider.SetupSequence(d => d.Create(It.IsAny<IIdentity>(), It.IsAny<ITokenProvider>(), It.IsAny<ITransportSettings[]>()))
                .Returns(client1)
                .Returns(client2);

            var credentialsCache = Mock.Of<ICredentialsCache>();
            var edgeHubIdentity = Mock.Of<IIdentity>(i => i.Id == "edgeDevice/$edgeHub");
            var cloudConnectionProvider = new CloudConnectionProvider(
                messageConverterProvider,
                1,
                deviceClientProvider.Object,
                Option.None<UpstreamProtocol>(),
                Mock.Of<ITokenProvider>(),
                Mock.Of<IDeviceScopeIdentitiesCache>(),
                credentialsCache,
                edgeHubIdentity,
                TimeSpan.FromMinutes(60),
                true,
                TimeSpan.FromSeconds(20),
                Option.None<IWebProxy>());
            cloudConnectionProvider.BindEdgeHub(Mock.Of<IEdgeHub>());
            IConnectionManager connectionManager = new ConnectionManager(cloudConnectionProvider, credentialsCache, GetIdentityProvider());

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
            var identity = new DeviceIdentity("iotHub", device);
            var deviceCredentials = new TokenCredentials(identity, "dummyToken", DummyProductInfo, true);

            Action<string, CloudConnectionStatus> callback = null;
            var cloudProxy = Mock.Of<ICloudProxy>(c => c.IsActive);
            var cloudConnection = Mock.Of<ICloudConnection>(
                cp => cp.IsActive && cp.CloseAsync() == Task.FromResult(true) && cp.CloudProxy == Option.Some(cloudProxy));
            var cloudProxyProviderMock = new Mock<ICloudConnectionProvider>();
            cloudProxyProviderMock.Setup(c => c.Connect(It.IsAny<IClientCredentials>(), It.IsAny<Action<string, CloudConnectionStatus>>()))
                .Callback<IClientCredentials, Action<string, CloudConnectionStatus>>((i, c) => callback = c)
                .ReturnsAsync(Try.Success(cloudConnection));

            var deviceProxy = new Mock<IDeviceProxy>(MockBehavior.Strict);
            deviceProxy.Setup(d => d.CloseAsync(It.Is<Exception>(e => e is EdgeHubConnectionException))).Returns(Task.CompletedTask);
            deviceProxy.SetupGet(d => d.IsActive).Returns(true);

            var credentialsCache = new Mock<ICredentialsCache>(MockBehavior.Strict);
            credentialsCache.Setup(c => c.Get(identity)).ReturnsAsync(Option.None<IClientCredentials>());
            var connectionManager = new ConnectionManager(cloudProxyProviderMock.Object, credentialsCache.Object, GetIdentityProvider());
            await connectionManager.AddDeviceConnection(deviceCredentials.Identity, deviceProxy.Object);
            Try<ICloudProxy> cloudProxyTry = await connectionManager.GetOrCreateCloudConnectionAsync(deviceCredentials);

            Assert.True(cloudProxyTry.Success);
            Assert.NotNull(callback);

            callback.Invoke(device, CloudConnectionStatus.TokenNearExpiry);
            deviceProxy.VerifyAll();
        }

        [Fact]
        [Unit]
        public async Task CloudProxyCallbackTest2()
        {
            string device = "device1";
            var deviceIdentity = new DeviceIdentity("iotHub", device);
            IClientCredentials deviceCredentials = new TokenCredentials(deviceIdentity, "dummyToken", DummyProductInfo, true);
            ITokenCredentials updatedDeviceCredentials = new TokenCredentials(deviceIdentity, "dummyToken", DummyProductInfo, true);

            Action<string, CloudConnectionStatus> callback = null;
            var cloudProxy = Mock.Of<ICloudProxy>(c => c.IsActive);
            var cloudConnection = Mock.Of<IClientTokenCloudConnection>(
                cp => cp.IsActive && cp.CloudProxy == Option.Some(cloudProxy));
            bool updatedCredentialsPassed = false;
            Mock.Get(cloudConnection).Setup(c => c.UpdateTokenAsync(updatedDeviceCredentials))
                .Callback(() => updatedCredentialsPassed = true)
                .ReturnsAsync(cloudProxy);
            var cloudProxyProviderMock = new Mock<ICloudConnectionProvider>();
            cloudProxyProviderMock.Setup(c => c.Connect(It.IsAny<IClientCredentials>(), It.IsAny<Action<string, CloudConnectionStatus>>()))
                .Callback<IClientCredentials, Action<string, CloudConnectionStatus>>((i, c) => callback = c)
                .ReturnsAsync(Try.Success(cloudConnection as ICloudConnection));

            var deviceProxy = new Mock<IDeviceProxy>(MockBehavior.Strict);

            var credentialsCache = new Mock<ICredentialsCache>(MockBehavior.Strict);
            credentialsCache.Setup(c => c.Get(deviceIdentity)).ReturnsAsync(Option.Some((IClientCredentials)updatedDeviceCredentials));
            var connectionManager = new ConnectionManager(cloudProxyProviderMock.Object, credentialsCache.Object, GetIdentityProvider());
            await connectionManager.AddDeviceConnection(deviceCredentials.Identity, deviceProxy.Object);
            Try<ICloudProxy> cloudProxyTry = await connectionManager.GetOrCreateCloudConnectionAsync(deviceCredentials);

            Assert.True(cloudProxyTry.Success);
            Assert.NotNull(callback);

            callback.Invoke(device, CloudConnectionStatus.TokenNearExpiry);

            await Task.Delay(TimeSpan.FromSeconds(2));
            deviceProxy.VerifyAll();
            credentialsCache.VerifyAll();
            Assert.True(updatedCredentialsPassed);
        }

        [Fact]
        [Unit]
        public async Task CloudConnectionUpdateTest()
        {
            ITokenProvider receivedTokenProvider = null;
            var messageConverterProvider = Mock.Of<IMessageConverterProvider>();
            var deviceClientProvider = new Mock<IClientProvider>();
            deviceClientProvider.Setup(d => d.Create(It.IsAny<IIdentity>(), It.IsAny<ITokenProvider>(), It.IsAny<ITransportSettings[]>()))
                .Callback<IIdentity, ITokenProvider, ITransportSettings[]>((i, s, t) => receivedTokenProvider = s)
                .Returns(() => GetDeviceClient());

            var credentialsCache = Mock.Of<ICredentialsCache>();
            var edgeHubIdentity = Mock.Of<IIdentity>(i => i.Id == "edgeDevice/$edgeHub");
            var cloudConnectionProvider = new CloudConnectionProvider(
                messageConverterProvider,
                1,
                deviceClientProvider.Object,
                Option.None<UpstreamProtocol>(),
                Mock.Of<ITokenProvider>(),
                Mock.Of<IDeviceScopeIdentitiesCache>(),
                credentialsCache,
                edgeHubIdentity,
                TimeSpan.FromMinutes(60),
                true,
                TimeSpan.FromSeconds(20),
                Option.None<IWebProxy>());
            cloudConnectionProvider.BindEdgeHub(Mock.Of<IEdgeHub>());
            IConnectionManager connectionManager = new ConnectionManager(cloudConnectionProvider, credentialsCache, GetIdentityProvider());

            string token1 = TokenHelper.CreateSasToken("foo.azure-devices.net", DateTime.UtcNow.AddHours(2));
            var deviceCredentials = new TokenCredentials(new DeviceIdentity("iotHub", "Device1"), token1, DummyProductInfo, true);
            var deviceProxy = Mock.Of<IDeviceProxy>(d => d.IsActive);

            Try<ICloudProxy> receivedCloudProxy1 = await connectionManager.CreateCloudConnectionAsync(deviceCredentials);
            await connectionManager.AddDeviceConnection(deviceCredentials.Identity, deviceProxy);

            Assert.True(receivedCloudProxy1.Success);
            Assert.NotNull(receivedCloudProxy1.Value);
            Assert.True(receivedCloudProxy1.Value.IsActive);
            Assert.NotNull(receivedTokenProvider);
            Assert.Equal(token1, receivedTokenProvider.GetTokenAsync(Option.None<TimeSpan>()).Result);
            Assert.Equal(deviceProxy, connectionManager.GetDeviceConnection(deviceCredentials.Identity.Id).OrDefault());

            string token2 = TokenHelper.CreateSasToken("foo.azure-devices.net", DateTime.UtcNow.AddHours(2));
            deviceCredentials = new TokenCredentials(new DeviceIdentity("iotHub", "Device1"), token2, DummyProductInfo, true);

            Try<ICloudProxy> receivedCloudProxy2 = await connectionManager.CreateCloudConnectionAsync(deviceCredentials);
            Assert.True(receivedCloudProxy2.Success);
            Assert.NotNull(receivedCloudProxy2.Value);
            Assert.True(receivedCloudProxy2.Value.IsActive);
            Assert.False(receivedCloudProxy1.Value.IsActive);
            Assert.NotNull(receivedTokenProvider);
            Assert.Equal(token2, receivedTokenProvider.GetTokenAsync(Option.None<TimeSpan>()).Result);
            Assert.Equal(deviceProxy, connectionManager.GetDeviceConnection(deviceCredentials.Identity.Id).OrDefault());
        }

        [Fact]
        [Unit]
        public async Task CloudConnectionInvalidUpdateTest()
        {
            var messageConverterProvider = Mock.Of<IMessageConverterProvider>();
            var deviceClientProvider = new Mock<IClientProvider>();
            deviceClientProvider.SetupSequence(d => d.Create(It.IsAny<IIdentity>(), It.IsAny<ITokenProvider>(), It.IsAny<ITransportSettings[]>()))
                .Returns(GetDeviceClient())
                .Throws(new UnauthorizedException("connstr2 is invalid!"))
                .Throws(new UnauthorizedException("connstr2 is invalid!"));

            var credentialsCache = Mock.Of<ICredentialsCache>();
            var edgeHubIdentity = Mock.Of<IIdentity>(i => i.Id == "edgeDevice/$edgeHub");
            var cloudConnectionProvider = new CloudConnectionProvider(
                messageConverterProvider,
                1,
                deviceClientProvider.Object,
                Option.None<UpstreamProtocol>(),
                Mock.Of<ITokenProvider>(),
                Mock.Of<IDeviceScopeIdentitiesCache>(),
                credentialsCache,
                edgeHubIdentity,
                TimeSpan.FromMinutes(60),
                true,
                TimeSpan.FromSeconds(20),
                Option.None<IWebProxy>());
            cloudConnectionProvider.BindEdgeHub(Mock.Of<IEdgeHub>());
            IConnectionManager connectionManager = new ConnectionManager(cloudConnectionProvider, credentialsCache, GetIdentityProvider());

            string token1 = TokenHelper.CreateSasToken("foo.azure-devices.net", DateTime.UtcNow.AddHours(2));
            var deviceCredentials1 = new TokenCredentials(new DeviceIdentity("iotHub", "Device1"), token1, DummyProductInfo, true);
            var deviceProxy = Mock.Of<IDeviceProxy>(d => d.IsActive);

            Try<ICloudProxy> receivedCloudProxy1 = await connectionManager.CreateCloudConnectionAsync(deviceCredentials1);
            await connectionManager.AddDeviceConnection(deviceCredentials1.Identity, deviceProxy);
            Assert.True(receivedCloudProxy1.Success);
            Assert.NotNull(receivedCloudProxy1.Value);
            Assert.True(receivedCloudProxy1.Value.IsActive);
            Assert.Equal(deviceProxy, connectionManager.GetDeviceConnection(deviceCredentials1.Identity.Id).OrDefault());

            string token2 = TokenHelper.CreateSasToken("foo.azure-devices.net", DateTime.UtcNow.AddHours(2));
            var deviceCredentials2 = new TokenCredentials(new DeviceIdentity("iotHub", "Device1"), token2, DummyProductInfo, true);

            Try<ICloudProxy> receivedCloudProxy2 = await connectionManager.CreateCloudConnectionAsync(deviceCredentials2);
            Assert.False(receivedCloudProxy2.Success);
            Assert.IsType<EdgeHubConnectionException>(receivedCloudProxy2.Exception);
            Assert.IsType<UnauthorizedException>(receivedCloudProxy2.Exception.InnerException);
            Assert.True(receivedCloudProxy1.Value.IsActive);
            Assert.Equal(deviceProxy, connectionManager.GetDeviceConnection(deviceCredentials2.Identity.Id).OrDefault());
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

            var deviceProxy1 = Mock.Of<IDeviceProxy>(d => d.IsActive);
            var deviceProxy2 = Mock.Of<IDeviceProxy>(d => d.IsActive);
            var deviceProxy3 = Mock.Of<IDeviceProxy>(d => d.IsActive);

            var credentialsCache = Mock.Of<ICredentialsCache>();
            var connectionManager = new ConnectionManager(cloudProviderMock.Object, credentialsCache, GetIdentityProvider(), 2);

            await connectionManager.AddDeviceConnection(deviceIdentity1, deviceProxy1);
            await connectionManager.AddDeviceConnection(deviceIdentity2, deviceProxy2);
            await Assert.ThrowsAsync<EdgeHubConnectionException>(async () => await connectionManager.AddDeviceConnection(deviceIdentity3, deviceProxy3));
        }

        [Fact]
        [Unit]
        public async Task AddRemoveSubscriptionsTest()
        {
            // Arrange
            string deviceId = "d1";
            var cloudConnectionProvider = Mock.Of<ICloudConnectionProvider>();
            var credentialsCache = Mock.Of<ICredentialsCache>();
            var connectionManager = new ConnectionManager(cloudConnectionProvider, credentialsCache, GetIdentityProvider());
            var identity = Mock.Of<IIdentity>(i => i.Id == deviceId);

            // Act
            await connectionManager.AddDeviceConnection(identity, Mock.Of<IDeviceProxy>(d => d.IsActive));
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
            Assert.True(subscriptions[DeviceSubscription.Methods]);
            Assert.True(subscriptions[DeviceSubscription.C2D]);

            // Act
            connectionManager.RemoveSubscription(deviceId, DeviceSubscription.Methods);
            connectionManager.RemoveSubscription(deviceId, DeviceSubscription.DesiredPropertyUpdates);
            subscriptionsOption = connectionManager.GetSubscriptions(deviceId);

            // Assert
            Assert.True(subscriptionsOption.HasValue);
            subscriptions = subscriptionsOption.OrDefault();
            Assert.Equal(3, subscriptions.Count);
            Assert.False(subscriptions[DeviceSubscription.Methods]);
            Assert.True(subscriptions[DeviceSubscription.C2D]);
            Assert.False(subscriptions[DeviceSubscription.DesiredPropertyUpdates]);
        }

        [Fact]
        [Unit]
        public async Task KeepSubscriptionsOnDeviceRemoveTest()
        {
            // Arrange
            string deviceId = "d1";
            var cloudConnectionProvider = Mock.Of<ICloudConnectionProvider>();
            var credentialsCache = Mock.Of<ICredentialsCache>();
            var connectionManager = new ConnectionManager(cloudConnectionProvider, credentialsCache, GetIdentityProvider());
            var identity = Mock.Of<IIdentity>(i => i.Id == deviceId);
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
            await connectionManager.AddDeviceConnection(identity, deviceProxy);
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
            Assert.True(subscriptions[DeviceSubscription.Methods]);
            Assert.True(subscriptions[DeviceSubscription.C2D]);

            // Act
            await connectionManager.RemoveDeviceConnection(deviceId);
            subscriptionsOption = connectionManager.GetSubscriptions(deviceId);

            // Assert
            Assert.False(subscriptionsOption.HasValue);

            // Act
            await connectionManager.AddDeviceConnection(identity, deviceProxy2);
            subscriptionsOption = connectionManager.GetSubscriptions(deviceId);

            // Assert
            Assert.True(subscriptionsOption.HasValue);
            subscriptions = subscriptionsOption.OrDefault();
            Assert.Equal(2, subscriptions.Count);
            Assert.True(subscriptions[DeviceSubscription.Methods]);
            Assert.True(subscriptions[DeviceSubscription.C2D]);

            // Act
            connectionManager.AddSubscription(deviceId, DeviceSubscription.DesiredPropertyUpdates);
            connectionManager.AddSubscription(deviceId, DeviceSubscription.ModuleMessages);
            subscriptionsOption = connectionManager.GetSubscriptions(deviceId);

            // Assert
            Assert.True(subscriptionsOption.HasValue);
            subscriptions = subscriptionsOption.OrDefault();
            Assert.Equal(4, subscriptions.Count);
            Assert.True(subscriptions[DeviceSubscription.Methods]);
            Assert.True(subscriptions[DeviceSubscription.C2D]);
            Assert.True(subscriptions[DeviceSubscription.DesiredPropertyUpdates]);
            Assert.True(subscriptions[DeviceSubscription.ModuleMessages]);
        }

        [Fact]
        [Unit]
        public async Task GetConnectedClientsTest()
        {
            // Arrange
            var cloudConnectionProvider = Mock.Of<ICloudConnectionProvider>();
            var credentialsCache = Mock.Of<ICredentialsCache>();
            var connectionManager = new ConnectionManager(cloudConnectionProvider, credentialsCache, GetIdentityProvider());

            var deviceProxies = new List<IDeviceProxy>();
            for (int i = 0; i < 10; i++)
            {
                string deviceId = $"device{i}";
                var identity = Mock.Of<IIdentity>(id => id.Id == deviceId);
                var deviceProxy = Mock.Of<IDeviceProxy>(d => d.Identity == identity && d.IsActive);
                Mock.Get(deviceProxy).Setup(d => d.CloseAsync(It.IsAny<Exception>()))
                    .Callback(() => Mock.Get(deviceProxy).SetupGet(dp => dp.IsActive).Returns(false))
                    .Returns(Task.CompletedTask);
                await connectionManager.AddDeviceConnection(identity, deviceProxy);
                deviceProxies.Add(deviceProxy);
            }

            var edgeHubIdentity = Mock.Of<IIdentity>(e => e.Id == $"{EdgeDeviceId}/{EdgeModuleId}");
            var edgeHubDeviceProxy = Mock.Of<IDeviceProxy>(e => e.Identity == edgeHubIdentity && e.IsActive);
            await connectionManager.AddDeviceConnection(edgeHubIdentity, edgeHubDeviceProxy);

            // Act
            IEnumerable<IIdentity> connectedClients = connectionManager.GetConnectedClients();

            // Assert
            Assert.NotNull(connectedClients);
            List<IIdentity> connectedClientsList = connectedClients.ToList();
            Assert.Equal(11, connectedClientsList.Count);
            Assert.Contains(connectedClientsList, c => c.Id.Equals($"{EdgeDeviceId}/{EdgeModuleId}"));

            for (int i = 0; i < 10; i++)
            {
                string deviceId = $"device{i}";
                Assert.Contains(connectedClientsList, c => c.Id.Equals(deviceId));
            }

            // Act
            for (int i = 0; i < 5; i++)
            {
                await deviceProxies[i].CloseAsync(new Exception());
            }

            connectedClients = connectionManager.GetConnectedClients();

            // Assert
            Assert.NotNull(connectedClients);
            connectedClientsList = connectedClients.ToList();
            Assert.Equal(6, connectedClientsList.Count);
            Assert.Contains(connectedClientsList, c => c.Id.Equals($"{EdgeDeviceId}/{EdgeModuleId}"));

            for (int i = 5; i < 10; i++)
            {
                string deviceId = $"device{i}";
                Assert.Contains(connectedClientsList, c => c.Id.Equals(deviceId));
            }
        }

        [Fact]
        [Unit]
        public async Task GetCloudProxyTest()
        {
            // Arrange
            string edgeDeviceId = "edgeDevice";
            string module1Id = "module1";
            string iotHub = "foo.azure-devices.net";
            string token = TokenHelper.CreateSasToken(iotHub);
            var module1Credentials = new TokenCredentials(new ModuleIdentity(iotHub, edgeDeviceId, module1Id), token, DummyProductInfo, true);

            IClient client1 = GetDeviceClient();
            IClient client2 = GetDeviceClient();
            var messageConverterProvider = Mock.Of<IMessageConverterProvider>();
            var deviceClientProvider = new Mock<IClientProvider>();
            deviceClientProvider.SetupSequence(d => d.Create(It.IsAny<IIdentity>(), It.IsAny<ITokenProvider>(), It.IsAny<ITransportSettings[]>()))
                .Returns(client1)
                .Returns(client2);

            ICredentialsCache credentialsCache = new CredentialsCache(new NullCredentialsCache());
            await credentialsCache.Add(module1Credentials);

            var cloudConnectionProvider = new CloudConnectionProvider(
                messageConverterProvider,
                1,
                deviceClientProvider.Object,
                Option.None<UpstreamProtocol>(),
                Mock.Of<ITokenProvider>(),
                Mock.Of<IDeviceScopeIdentitiesCache>(),
                credentialsCache,
                new ModuleIdentity(iotHub, edgeDeviceId, "$edgeHub"),
                TimeSpan.FromMinutes(60),
                true,
                TimeSpan.FromSeconds(20),
                Option.None<IWebProxy>());
            cloudConnectionProvider.BindEdgeHub(Mock.Of<IEdgeHub>());
            IConnectionManager connectionManager = new ConnectionManager(cloudConnectionProvider, credentialsCache, GetIdentityProvider());

            // Act
            Option<ICloudProxy> getCloudProxyTask = await connectionManager.GetCloudConnection(module1Credentials.Identity.Id);

            // Assert
            Assert.True(getCloudProxyTask.HasValue);
            Assert.True(getCloudProxyTask.OrDefault().IsActive);

            // Act
            await getCloudProxyTask.OrDefault().CloseAsync();
            Option<ICloudProxy> newCloudProxyTask1 = await connectionManager.GetCloudConnection(module1Credentials.Identity.Id);

            // Assert
            Assert.True(newCloudProxyTask1.HasValue);
            Assert.NotEqual(newCloudProxyTask1.OrDefault(), getCloudProxyTask.OrDefault());

            Mock.Get(client1).Verify(cp => cp.CloseAsync(), Times.Once);
            Mock.Get(client2).Verify(cp => cp.CloseAsync(), Times.Never);
        }

        [Fact]
        [Unit]
        public async Task GetMultipleCloudProxiesTest()
        {
            // Arrange
            string edgeDeviceId = "edgeDevice";
            string module1Id = "module1";
            string token = TokenHelper.CreateSasToken(IotHubHostName);
            var module1Credentials = new TokenCredentials(new ModuleIdentity(IotHubHostName, edgeDeviceId, module1Id), token, DummyProductInfo, true);
            IClient client1 = GetDeviceClient();
            IClient client2 = GetDeviceClient();
            var messageConverterProvider = Mock.Of<IMessageConverterProvider>();
            var deviceClientProvider = new Mock<IClientProvider>();
            deviceClientProvider.SetupSequence(d => d.Create(It.IsAny<IIdentity>(), It.IsAny<ITokenProvider>(), It.IsAny<ITransportSettings[]>()))
                .Returns(client1)
                .Returns(client2);

            ICredentialsCache credentialsCache = new CredentialsCache(new NullCredentialsCache());
            await credentialsCache.Add(module1Credentials);

            var cloudConnectionProvider = new CloudConnectionProvider(
                messageConverterProvider,
                1,
                deviceClientProvider.Object,
                Option.None<UpstreamProtocol>(),
                Mock.Of<ITokenProvider>(),
                Mock.Of<IDeviceScopeIdentitiesCache>(),
                credentialsCache,
                new ModuleIdentity(IotHubHostName, edgeDeviceId, "$edgeHub"),
                TimeSpan.FromMinutes(60),
                true,
                TimeSpan.FromSeconds(20),
                Option.None<IWebProxy>());
            cloudConnectionProvider.BindEdgeHub(Mock.Of<IEdgeHub>());
            IConnectionManager connectionManager = new ConnectionManager(cloudConnectionProvider, credentialsCache, GetIdentityProvider());

            // Act
            Task<Option<ICloudProxy>> getCloudProxyTask1 = connectionManager.GetCloudConnection(module1Credentials.Identity.Id);
            Task<Option<ICloudProxy>> getCloudProxyTask2 = connectionManager.GetCloudConnection(module1Credentials.Identity.Id);
            Task<Option<ICloudProxy>> getCloudProxyTask3 = connectionManager.GetCloudConnection(module1Credentials.Identity.Id);
            Task<Option<ICloudProxy>> getCloudProxyTask4 = connectionManager.GetCloudConnection(module1Credentials.Identity.Id);
            Option<ICloudProxy>[] cloudProxies = await Task.WhenAll(getCloudProxyTask1, getCloudProxyTask2, getCloudProxyTask3, getCloudProxyTask4);

            // Assert
            Assert.True(cloudProxies[0].HasValue);
            Assert.True(cloudProxies[1].HasValue);
            Assert.True(cloudProxies[2].HasValue);
            Assert.True(cloudProxies[3].HasValue);
            Assert.Equal(cloudProxies[0].OrDefault(), cloudProxies[1].OrDefault());
            Assert.Equal(cloudProxies[0].OrDefault(), cloudProxies[2].OrDefault());
            Assert.Equal(cloudProxies[0].OrDefault(), cloudProxies[3].OrDefault());

            // Act
            await cloudProxies[0].OrDefault().CloseAsync();
            Option<ICloudProxy> newCloudProxyTask1 = await connectionManager.GetCloudConnection(module1Credentials.Identity.Id);

            // Assert
            Assert.True(newCloudProxyTask1.HasValue);
            Assert.NotEqual(newCloudProxyTask1.OrDefault(), cloudProxies[0].OrDefault());
            Mock.Get(client1).Verify(cp => cp.CloseAsync(), Times.Once);
            Mock.Get(client2).Verify(cp => cp.CloseAsync(), Times.Never);
        }

        static ICloudConnection GetCloudConnectionMock()
        {
            ICloudProxy cloudProxyMock = GetCloudProxyMock();
            var cloudConnectionMock = new Mock<IClientTokenCloudConnection>();
            cloudConnectionMock.SetupGet(dp => dp.IsActive).Returns(true);
            cloudConnectionMock.SetupGet(dp => dp.CloudProxy).Returns(Option.Some(cloudProxyMock));
            cloudConnectionMock.Setup(c => c.UpdateTokenAsync(It.IsAny<ITokenCredentials>()))
                .Callback(
                    () =>
                    {
                        cloudProxyMock = GetCloudProxyMock();
                        cloudConnectionMock.SetupGet(dp => dp.CloudProxy).Returns(Option.Some(cloudProxyMock));
                    })
                .ReturnsAsync(cloudProxyMock);
            cloudConnectionMock.Setup(dp => dp.CloseAsync()).Returns(Task.FromResult(true))
                .Callback(
                    () =>
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
            deviceClient.Setup(dc => dc.OpenAsync()).Returns(Task.CompletedTask);
            return deviceClient.Object;
        }

        static IIdentityProvider GetIdentityProvider() => new IdentityProvider(IotHubHostName);
    }
}
