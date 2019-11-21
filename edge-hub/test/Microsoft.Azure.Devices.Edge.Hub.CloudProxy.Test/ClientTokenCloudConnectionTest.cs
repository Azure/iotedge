// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy.Test
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Exceptions;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Shared;
    using Moq;
    using Xunit;
    using TransportType = Microsoft.Azure.Devices.Client.TransportType;

    [Unit]
    public class ClientTokenCloudConnectionTest
    {
        const string DummyProductInfo = "IoTEdge 1.0.6 1.20.0-RC2";
        static readonly ITokenProvider TokenProvider = Mock.Of<ITokenProvider>();
        static readonly IDeviceScopeIdentitiesCache DeviceScopeIdentitiesCache = Mock.Of<IDeviceScopeIdentitiesCache>();

        [Unit]
        [Fact]
        public async Task GetCloudConnectionForIdentityWithTokenTest()
        {
            IClientProvider clientProvider = GetMockDeviceClientProviderWithToken();
            var transportSettings = new ITransportSettings[] { new AmqpTransportSettings(TransportType.Amqp_Tcp_Only) };
            var messageConverterProvider = new MessageConverterProvider(new Dictionary<Type, IMessageConverter> { [typeof(TwinCollection)] = Mock.Of<IMessageConverter>() });
            ITokenCredentials clientCredentials1 = GetMockClientCredentialsWithToken();
            ClientTokenCloudConnection cloudConnection = await ClientTokenCloudConnection.Create(
                clientCredentials1,
                (_, __) => { },
                transportSettings,
                messageConverterProvider,
                clientProvider,
                Mock.Of<ICloudListener>(),
                TimeSpan.FromMinutes(60),
                true,
                TimeSpan.FromSeconds(20),
                DummyProductInfo);
            Option<ICloudProxy> cloudProxy1 = cloudConnection.CloudProxy;
            Assert.True(cloudProxy1.HasValue);
            Assert.True(cloudProxy1.OrDefault().IsActive);

            ITokenCredentials clientCredentials2 = GetMockClientCredentialsWithToken();
            ICloudProxy cloudProxy2 = await cloudConnection.UpdateTokenAsync(clientCredentials2);
            Assert.Equal(cloudProxy2, cloudConnection.CloudProxy.OrDefault());
            Assert.True(cloudProxy2.IsActive);
            Assert.False(cloudProxy1.OrDefault().IsActive);
            Assert.NotEqual(cloudProxy1.OrDefault(), cloudProxy2);
        }

        [Unit]
        [Fact]
        public async Task UpdateInvalidIdentityWithTokenTest()
        {
            var deviceClientProvider = new Mock<IClientProvider>();
            deviceClientProvider.SetupSequence(dc => dc.Create(It.IsAny<IIdentity>(), It.IsAny<ITokenProvider>(), It.IsAny<ITransportSettings[]>()))
                .Returns(GetMockDeviceClient())
                .Throws(new UnauthorizedException("Unauthorized"));

            var transportSettings = new ITransportSettings[] { new AmqpTransportSettings(TransportType.Amqp_Tcp_Only) };

            var messageConverterProvider = new MessageConverterProvider(new Dictionary<Type, IMessageConverter> { [typeof(TwinCollection)] = Mock.Of<IMessageConverter>() });
            ITokenCredentials identity1 = GetMockClientCredentialsWithToken();
            ClientTokenCloudConnection cloudConnection = await ClientTokenCloudConnection.Create(
                identity1,
                (_, __) => { },
                transportSettings,
                messageConverterProvider,
                deviceClientProvider.Object,
                Mock.Of<ICloudListener>(),
                TimeSpan.FromMinutes(60),
                true,
                TimeSpan.FromSeconds(20),
                DummyProductInfo);

            Option<ICloudProxy> cloudProxy1 = cloudConnection.CloudProxy;
            Assert.True(cloudProxy1.HasValue);
            Assert.True(cloudProxy1.OrDefault().IsActive);

            ITokenCredentials identity2 = GetMockClientCredentialsWithToken();
            await Assert.ThrowsAsync<UnauthorizedException>(() => cloudConnection.UpdateTokenAsync(identity2));
            Assert.True(cloudProxy1.OrDefault().IsActive);
        }

        [Fact]
        [Unit]
        public async Task InitializeAndGetCloudProxyTest()
        {
            string iothubHostName = "test.azure-devices.net";
            string deviceId = "device1";

            ITokenCredentials GetClientCredentialsWithNonExpiringToken()
            {
                string token = TokenHelper.CreateSasToken(iothubHostName, DateTime.UtcNow.AddMinutes(10));
                var identity = new DeviceIdentity(iothubHostName, deviceId);
                return new TokenCredentials(identity, token, string.Empty, false);
            }

            IClient client = GetMockDeviceClient();
            var deviceClientProvider = new Mock<IClientProvider>();
            deviceClientProvider.Setup(dc => dc.Create(It.IsAny<IIdentity>(), It.IsAny<ITokenProvider>(), It.IsAny<ITransportSettings[]>()))
                .Returns(() => client);

            var transportSettings = new ITransportSettings[] { new AmqpTransportSettings(TransportType.Amqp_Tcp_Only) };
            var messageConverterProvider = new MessageConverterProvider(new Dictionary<Type, IMessageConverter> { [typeof(TwinCollection)] = Mock.Of<IMessageConverter>() });

            ITokenCredentials clientCredentialsWithNonExpiringToken = GetClientCredentialsWithNonExpiringToken();
            ClientTokenCloudConnection cloudConnection = await ClientTokenCloudConnection.Create(
                clientCredentialsWithNonExpiringToken,
                (_, __) => { },
                transportSettings,
                messageConverterProvider,
                deviceClientProvider.Object,
                Mock.Of<ICloudListener>(),
                TimeSpan.FromMinutes(60),
                true,
                TimeSpan.FromSeconds(20),
                DummyProductInfo);

            Option<ICloudProxy> cloudProxy = cloudConnection.CloudProxy;
            Assert.True(cloudProxy.HasValue);
            Assert.True(cloudProxy.OrDefault().IsActive);
            Mock.Get(client).Verify(c => c.OpenAsync(), Times.Once);
        }

        [Fact]
        [Unit]
        public async Task RefreshTokenTest()
        {
            string iothubHostName = "test.azure-devices.net";
            string deviceId = "device1";

            ITokenCredentials GetClientCredentialsWithExpiringToken()
            {
                string token = TokenHelper.CreateSasToken(iothubHostName, DateTime.UtcNow.AddMinutes(3));
                var identity = new DeviceIdentity(iothubHostName, deviceId);
                return new TokenCredentials(identity, token, string.Empty, false);
            }

            ITokenCredentials GetClientCredentialsWithNonExpiringToken()
            {
                string token = TokenHelper.CreateSasToken(iothubHostName, DateTime.UtcNow.AddMinutes(10));
                var identity = new DeviceIdentity(iothubHostName, deviceId);
                return new TokenCredentials(identity, token, string.Empty, false);
            }

            ITokenProvider tokenProvider = null;
            IClientProvider clientProvider = GetMockDeviceClientProviderWithToken((s, a, t) => tokenProvider = a);

            var transportSettings = new ITransportSettings[] { new AmqpTransportSettings(TransportType.Amqp_Tcp_Only) };

            var receivedStatus = CloudConnectionStatus.ConnectionEstablished;
            void ConnectionStatusHandler(string id, CloudConnectionStatus status) => receivedStatus = status;
            var messageConverterProvider = new MessageConverterProvider(new Dictionary<Type, IMessageConverter> { [typeof(TwinCollection)] = Mock.Of<IMessageConverter>() });

            ITokenCredentials clientCredentialsWithExpiringToken1 = GetClientCredentialsWithExpiringToken();
            ClientTokenCloudConnection cloudConnection = await ClientTokenCloudConnection.Create(
                clientCredentialsWithExpiringToken1,
                ConnectionStatusHandler,
                transportSettings,
                messageConverterProvider,
                clientProvider,
                Mock.Of<ICloudListener>(),
                TimeSpan.FromMinutes(60),
                true,
                TimeSpan.FromSeconds(20),
                DummyProductInfo);

            Option<ICloudProxy> cloudProxy1 = cloudConnection.CloudProxy;
            Assert.True(cloudProxy1.HasValue);
            Assert.True(cloudProxy1.OrDefault().IsActive);

            Assert.NotNull(tokenProvider);

            Task<string> getTokenTask = tokenProvider.GetTokenAsync(Option.None<TimeSpan>());
            Assert.False(getTokenTask.IsCompleted);

            Assert.Equal(CloudConnectionStatus.TokenNearExpiry, receivedStatus);

            ITokenCredentials clientCredentialsWithExpiringToken2 = GetClientCredentialsWithNonExpiringToken();
            ICloudProxy cloudProxy2 = await cloudConnection.UpdateTokenAsync(clientCredentialsWithExpiringToken2);

            // Wait for the task to complete
            await Task.Delay(TimeSpan.FromSeconds(10));

            Assert.True(getTokenTask.IsCompletedSuccessfully);
            Assert.Equal(cloudProxy2, cloudConnection.CloudProxy.OrDefault());
            Assert.True(cloudProxy2.IsActive);
            Assert.True(cloudProxy1.OrDefault().IsActive);
            Assert.Equal(cloudProxy1.OrDefault(), cloudProxy2);
            Assert.Equal(getTokenTask.Result, clientCredentialsWithExpiringToken2.Token);
        }

        [Fact]
        [Unit]
        public async Task RefreshTokenWithRetryTest()
        {
            string iothubHostName = "test.azure-devices.net";
            string deviceId = "device1";

            ITokenCredentials GetClientCredentialsWithExpiringToken()
            {
                string token = TokenHelper.CreateSasToken(iothubHostName, DateTime.UtcNow.AddMinutes(3));
                var identity = new DeviceIdentity(iothubHostName, deviceId);
                return new TokenCredentials(identity, token, string.Empty, false);
            }

            ITokenCredentials GetClientCredentialsWithNonExpiringToken()
            {
                string token = TokenHelper.CreateSasToken(iothubHostName, DateTime.UtcNow.AddMinutes(10));
                var identity = new DeviceIdentity(iothubHostName, deviceId);
                return new TokenCredentials(identity, token, string.Empty, false);
            }

            ITokenProvider tokenProvider = null;
            IClientProvider clientProvider = GetMockDeviceClientProviderWithToken((s, a, t) => tokenProvider = a);

            var transportSettings = new ITransportSettings[] { new AmqpTransportSettings(TransportType.Amqp_Tcp_Only) };

            var receivedStatuses = new List<CloudConnectionStatus>();
            void ConnectionStatusHandler(string id, CloudConnectionStatus status) => receivedStatuses.Add(status);
            var messageConverterProvider = new MessageConverterProvider(new Dictionary<Type, IMessageConverter> { [typeof(TwinCollection)] = Mock.Of<IMessageConverter>() });

            ITokenCredentials clientCredentialsWithExpiringToken1 = GetClientCredentialsWithExpiringToken();
            ClientTokenCloudConnection cloudConnection = await ClientTokenCloudConnection.Create(
                clientCredentialsWithExpiringToken1,
                ConnectionStatusHandler,
                transportSettings,
                messageConverterProvider,
                clientProvider,
                Mock.Of<ICloudListener>(),
                TimeSpan.FromMinutes(60),
                true,
                TimeSpan.FromSeconds(20),
                DummyProductInfo);

            Option<ICloudProxy> cloudProxy1 = cloudConnection.CloudProxy;
            Assert.True(cloudProxy1.HasValue);
            Assert.True(cloudProxy1.OrDefault().IsActive);

            Assert.NotNull(tokenProvider);

            // Try to refresh token but get an expiring token
            Task<string> getTokenTask = tokenProvider.GetTokenAsync(Option.None<TimeSpan>());
            Assert.False(getTokenTask.IsCompleted);

            Assert.Single(receivedStatuses);
            Assert.Equal(CloudConnectionStatus.TokenNearExpiry, receivedStatuses[0]);

            ICloudProxy cloudProxy2 = await cloudConnection.UpdateTokenAsync(clientCredentialsWithExpiringToken1);

            // Wait for the task to process
            await Task.Delay(TimeSpan.FromSeconds(5));

            Assert.False(getTokenTask.IsCompletedSuccessfully);
            Assert.Equal(cloudProxy2, cloudConnection.CloudProxy.OrDefault());
            Assert.True(cloudProxy2.IsActive);
            Assert.True(cloudProxy1.OrDefault().IsActive);
            Assert.Equal(cloudProxy1.OrDefault(), cloudProxy2);

            // Wait for 20 secs for retry to happen
            await Task.Delay(TimeSpan.FromSeconds(20));

            // Check if retry happened
            Assert.Equal(2, receivedStatuses.Count);
            Assert.Equal(CloudConnectionStatus.TokenNearExpiry, receivedStatuses[1]);

            ITokenCredentials clientCredentialsWithNonExpiringToken = GetClientCredentialsWithNonExpiringToken();
            ICloudProxy cloudProxy3 = await cloudConnection.UpdateTokenAsync(clientCredentialsWithNonExpiringToken);

            // Wait for the task to complete
            await Task.Delay(TimeSpan.FromSeconds(5));

            Assert.True(getTokenTask.IsCompletedSuccessfully);
            Assert.Equal(cloudProxy3, cloudConnection.CloudProxy.OrDefault());
            Assert.True(cloudProxy3.IsActive);
            Assert.True(cloudProxy1.OrDefault().IsActive);
            Assert.Equal(cloudProxy1.OrDefault(), cloudProxy3);
            Assert.Equal(getTokenTask.Result, clientCredentialsWithNonExpiringToken.Token);
        }

        [Fact]
        [Unit]
        public async Task CloudConnectionCallbackTest()
        {
            int receivedConnectedStatusCount = 0;
            ConnectionStatusChangesHandler connectionStatusChangesHandler = (_, __) => { };

            IClient GetMockedDeviceClient()
            {
                var deviceClient = new Mock<IClient>();
                deviceClient.SetupGet(dc => dc.IsActive).Returns(true);
                deviceClient.Setup(dc => dc.CloseAsync())
                    .Callback(() => deviceClient.SetupGet(dc => dc.IsActive).Returns(false))
                    .Returns(Task.FromResult(true));

                deviceClient.Setup(dc => dc.SetConnectionStatusChangedHandler(It.IsAny<ConnectionStatusChangesHandler>()))
                    .Callback<ConnectionStatusChangesHandler>(c => connectionStatusChangesHandler = c);

                deviceClient.Setup(dc => dc.OpenAsync())
                    .Callback(
                        () =>
                        {
                            Assert.NotNull(connectionStatusChangesHandler);
                            connectionStatusChangesHandler.Invoke(ConnectionStatus.Connected, ConnectionStatusChangeReason.Connection_Ok);
                        })
                    .Returns(Task.CompletedTask);
                return deviceClient.Object;
            }

            var deviceClientProvider = new Mock<IClientProvider>();
            deviceClientProvider.Setup(dc => dc.Create(It.IsAny<IIdentity>(), It.IsAny<ITokenProvider>(), It.IsAny<ITransportSettings[]>()))
                .Returns(() => GetMockedDeviceClient());

            var transportSettings = new ITransportSettings[] { new AmqpTransportSettings(TransportType.Amqp_Tcp_Only) };

            void ConnectionStatusHandler(string id, CloudConnectionStatus status)
            {
                if (status == CloudConnectionStatus.ConnectionEstablished)
                {
                    receivedConnectedStatusCount++;
                }
            }

            var messageConverterProvider = new MessageConverterProvider(new Dictionary<Type, IMessageConverter> { [typeof(TwinCollection)] = Mock.Of<IMessageConverter>() });
            ITokenCredentials clientCredentialsWithExpiringToken1 = GetMockClientCredentialsWithToken();
            ClientTokenCloudConnection cloudConnection = await ClientTokenCloudConnection.Create(
                clientCredentialsWithExpiringToken1,
                ConnectionStatusHandler,
                transportSettings,
                messageConverterProvider,
                deviceClientProvider.Object,
                Mock.Of<ICloudListener>(),
                TimeSpan.FromMinutes(60),
                true,
                TimeSpan.FromSeconds(20),
                DummyProductInfo);

            Assert.Equal(1, receivedConnectedStatusCount);
            Option<ICloudProxy> cloudProxy1 = cloudConnection.CloudProxy;
            Assert.True(cloudProxy1.HasValue);
            Assert.True(cloudProxy1.OrDefault().IsActive);

            Assert.NotNull(connectionStatusChangesHandler);
            connectionStatusChangesHandler.Invoke(ConnectionStatus.Connected, ConnectionStatusChangeReason.Connection_Ok);
            Assert.Equal(2, receivedConnectedStatusCount);

            ITokenCredentials clientCredentialsWithExpiringToken2 = GetMockClientCredentialsWithToken();
            ICloudProxy cloudProxy2 = await cloudConnection.UpdateTokenAsync(clientCredentialsWithExpiringToken2);
            Assert.True(cloudProxy2.IsActive);
            Assert.Equal(cloudProxy2, cloudConnection.CloudProxy.OrDefault());
            Assert.Equal(2, receivedConnectedStatusCount);

            connectionStatusChangesHandler.Invoke(ConnectionStatus.Connected, ConnectionStatusChangeReason.Connection_Ok);
            Assert.Equal(3, receivedConnectedStatusCount);
        }

        [Unit]
        [Fact]
        public async Task UpdateDeviceConnectionTest()
        {
            int receivedConnectedStatusCount = 0;
            ConnectionStatusChangesHandler connectionStatusChangesHandler = null;
            string hostname = "dummy.azure-devices.net";
            string deviceId = "device1";

            ITokenCredentials GetClientCredentials(TimeSpan tokenExpiryDuration)
            {
                string token = TokenHelper.CreateSasToken(hostname, DateTime.UtcNow.AddSeconds(tokenExpiryDuration.TotalSeconds));
                var identity = new DeviceIdentity(hostname, deviceId);
                return new TokenCredentials(identity, token, string.Empty, false);
            }

            IDeviceProxy GetMockDeviceProxy()
            {
                var deviceProxyMock1 = new Mock<IDeviceProxy>();
                deviceProxyMock1.SetupGet(dp => dp.IsActive).Returns(true);
                deviceProxyMock1.Setup(dp => dp.CloseAsync(It.IsAny<Exception>()))
                    .Callback(() => deviceProxyMock1.SetupGet(dp => dp.IsActive).Returns(false))
                    .Returns(Task.CompletedTask);
                return deviceProxyMock1.Object;
            }

            IClient GetMockedDeviceClient()
            {
                var deviceClient = new Mock<IClient>();
                deviceClient.SetupGet(dc => dc.IsActive).Returns(true);
                deviceClient.Setup(dc => dc.CloseAsync())
                    .Callback(() => deviceClient.SetupGet(dc => dc.IsActive).Returns(false))
                    .Returns(Task.FromResult(true));

                deviceClient.Setup(dc => dc.SetConnectionStatusChangedHandler(It.IsAny<ConnectionStatusChangesHandler>()))
                    .Callback<ConnectionStatusChangesHandler>(c => connectionStatusChangesHandler = c);

                deviceClient.Setup(dc => dc.OpenAsync())
                    .Callback(
                        () =>
                        {
                            int currentCount = receivedConnectedStatusCount;
                            Assert.NotNull(connectionStatusChangesHandler);
                            connectionStatusChangesHandler.Invoke(ConnectionStatus.Connected, ConnectionStatusChangeReason.Connection_Ok);
                            Assert.Equal(receivedConnectedStatusCount, currentCount);
                        })
                    .Returns(Task.CompletedTask);
                return deviceClient.Object;
            }

            ITokenProvider tokenProvider = null;
            var deviceClientProvider = new Mock<IClientProvider>();
            deviceClientProvider.Setup(dc => dc.Create(It.IsAny<IIdentity>(), It.IsAny<ITokenProvider>(), It.IsAny<ITransportSettings[]>()))
                .Callback<IIdentity, ITokenProvider, ITransportSettings[]>((s, a, t) => tokenProvider = a)
                .Returns(GetMockedDeviceClient);

            var productInfoStore = Mock.Of<IProductInfoStore>();
            var messageConverterProvider = Mock.Of<IMessageConverterProvider>();

            var credentialsCache = Mock.Of<ICredentialsCache>();
            ICloudConnectionProvider cloudConnectionProvider = new CloudConnectionProvider(
                messageConverterProvider,
                1,
                deviceClientProvider.Object,
                Option.None<UpstreamProtocol>(),
                TokenProvider,
                DeviceScopeIdentitiesCache,
                credentialsCache,
                Mock.Of<IIdentity>(i => i.Id == $"{deviceId}/$edgeHub"),
                TimeSpan.FromMinutes(60),
                true,
                TimeSpan.FromSeconds(20),
                Option.None<IWebProxy>(),
                productInfoStore);
            cloudConnectionProvider.BindEdgeHub(Mock.Of<IEdgeHub>());
            IConnectionManager connectionManager = new ConnectionManager(cloudConnectionProvider, Mock.Of<ICredentialsCache>(), new IdentityProvider(hostname));

            ITokenCredentials clientCredentials1 = GetClientCredentials(TimeSpan.FromSeconds(10));
            Try<ICloudProxy> cloudProxyTry1 = await connectionManager.CreateCloudConnectionAsync(clientCredentials1);
            Assert.True(cloudProxyTry1.Success);

            IDeviceProxy deviceProxy1 = GetMockDeviceProxy();
            await connectionManager.AddDeviceConnection(clientCredentials1.Identity, deviceProxy1);

            await Task.Delay(TimeSpan.FromSeconds(10));
            Assert.NotNull(tokenProvider);
            Task<string> tokenGetter = tokenProvider.GetTokenAsync(Option.None<TimeSpan>());
            Assert.False(tokenGetter.IsCompleted);

            ITokenCredentials clientCredentials2 = GetClientCredentials(TimeSpan.FromMinutes(2));
            Try<ICloudProxy> cloudProxyTry2 = await connectionManager.CreateCloudConnectionAsync(clientCredentials2);
            Assert.True(cloudProxyTry2.Success);

            IDeviceProxy deviceProxy2 = GetMockDeviceProxy();
            await connectionManager.AddDeviceConnection(clientCredentials2.Identity, deviceProxy2);

            await Task.Delay(TimeSpan.FromSeconds(3));
            Assert.False(tokenGetter.IsCompleted);

            ITokenCredentials clientCredentials3 = GetClientCredentials(TimeSpan.FromMinutes(10));
            Try<ICloudProxy> cloudProxyTry3 = await connectionManager.CreateCloudConnectionAsync(clientCredentials3);
            Assert.True(cloudProxyTry3.Success);

            IDeviceProxy deviceProxy3 = GetMockDeviceProxy();
            await connectionManager.AddDeviceConnection(clientCredentials3.Identity, deviceProxy3);

            await Task.Delay(TimeSpan.FromSeconds(23));
            Assert.True(tokenGetter.IsCompleted);
            Assert.Equal(tokenGetter.Result, clientCredentials3.Token);
        }

        static IClientProvider GetMockDeviceClientProviderWithToken(Action<IIdentity, ITokenProvider, ITransportSettings[]> callback = null)
        {
            var deviceClientProvider = new Mock<IClientProvider>();
            deviceClientProvider.Setup(dc => dc.Create(It.IsAny<IIdentity>(), It.IsAny<ITokenProvider>(), It.IsAny<ITransportSettings[]>()))
                .Callback<IIdentity, ITokenProvider, ITransportSettings[]>((c, p, t) => callback?.Invoke(c, p, t))
                .Returns(() => GetMockDeviceClient());
            return deviceClientProvider.Object;
        }

        static IClient GetMockDeviceClient()
        {
            var deviceClient = new Mock<IClient>();
            deviceClient.SetupGet(dc => dc.IsActive).Returns(true);
            deviceClient.Setup(dc => dc.CloseAsync())
                .Callback(() => deviceClient.SetupGet(dc => dc.IsActive).Returns(false))
                .Returns(Task.FromResult(true));
            deviceClient.Setup(dc => dc.OpenAsync()).Returns(Task.CompletedTask);
            return deviceClient.Object;
        }

        static ITokenCredentials GetMockClientCredentialsWithToken(
            string hostname = "dummy.azure-devices.net",
            string deviceId = "device1")
        {
            string token = TokenHelper.CreateSasToken(hostname);
            var identity = new DeviceIdentity(hostname, deviceId);
            return new TokenCredentials(identity, token, string.Empty, false);
        }
    }
}
