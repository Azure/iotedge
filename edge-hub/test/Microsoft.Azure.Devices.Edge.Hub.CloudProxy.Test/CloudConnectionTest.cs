// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy.Test
{
    using System;
    using System.Collections.Generic;
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

    public class CloudConnectionTest
    {
        static readonly ITokenProvider TokenProvider = Mock.Of<ITokenProvider>();
        static readonly IDeviceScopeIdentitiesCache DeviceScopeIdentitiesCache = Mock.Of<IDeviceScopeIdentitiesCache>();

        [Fact]
        [Unit]
        public void CheckTokenExpiredTest()
        {
            // Arrange
            string hostname = "dummy.azure-devices.net";
            DateTime expiryTime = DateTime.UtcNow.AddHours(2);
            string validToken = TokenHelper.CreateSasToken(hostname, expiryTime);

            // Act
            DateTime actualExpiryTime = CloudConnection.GetTokenExpiry(hostname, validToken);

            // Assert
            Assert.True(actualExpiryTime - expiryTime < TimeSpan.FromSeconds(1));

            // Arrange
            expiryTime = DateTime.UtcNow.AddHours(-2);
            string expiredToken = TokenHelper.CreateSasToken(hostname, expiryTime);

            // Act
            actualExpiryTime = CloudConnection.GetTokenExpiry(hostname, expiredToken);

            // Assert
            Assert.Equal(DateTime.MinValue, actualExpiryTime);
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
                            int currentCount = receivedConnectedStatusCount;
                            Assert.NotNull(connectionStatusChangesHandler);
                            connectionStatusChangesHandler.Invoke(ConnectionStatus.Connected, ConnectionStatusChangeReason.Connection_Ok);
                            Assert.Equal(receivedConnectedStatusCount, currentCount);
                        })
                    .Returns(Task.CompletedTask);
                return deviceClient.Object;
            }

            var deviceClientProvider = new Mock<IClientProvider>();
            deviceClientProvider.Setup(dc => dc.Create(It.IsAny<IIdentity>(), It.IsAny<IAuthenticationMethod>(), It.IsAny<ITransportSettings[]>()))
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
            var cloudConnection = new CloudConnection(ConnectionStatusHandler, transportSettings, messageConverterProvider, deviceClientProvider.Object, Mock.Of<ICloudListener>(), TokenProvider, DeviceScopeIdentitiesCache, TimeSpan.FromMinutes(60), true);

            IClientCredentials clientCredentialsWithExpiringToken1 = GetMockClientCredentialsWithToken();
            Assert.Equal(receivedConnectedStatusCount, 0);
            ICloudProxy cloudProxy1 = await cloudConnection.CreateOrUpdateAsync(clientCredentialsWithExpiringToken1);
            Assert.True(cloudProxy1.IsActive);
            Assert.Equal(cloudProxy1, cloudConnection.CloudProxy.OrDefault());
            Assert.Equal(receivedConnectedStatusCount, 0);

            Assert.NotNull(connectionStatusChangesHandler);
            connectionStatusChangesHandler.Invoke(ConnectionStatus.Connected, ConnectionStatusChangeReason.Connection_Ok);
            Assert.Equal(receivedConnectedStatusCount, 1);

            IClientCredentials clientCredentialsWithExpiringToken2 = GetMockClientCredentialsWithToken();
            ICloudProxy cloudProxy2 = await cloudConnection.CreateOrUpdateAsync(clientCredentialsWithExpiringToken2);
            Assert.True(cloudProxy2.IsActive);
            Assert.Equal(cloudProxy2, cloudConnection.CloudProxy.OrDefault());
            Assert.Equal(receivedConnectedStatusCount, 1);

            connectionStatusChangesHandler.Invoke(ConnectionStatus.Connected, ConnectionStatusChangeReason.Connection_Ok);
            Assert.Equal(receivedConnectedStatusCount, 2);
        }

        [Unit]
        [Fact]
        public Task GetCloudConnectionForIdentityWithKeyTest()
        {
            IClientProvider clientProvider = GetMockDeviceClientProviderWithKey();
            return GetCloudConnectionTest(() => GetMockClientCredentialsWithKey(), clientProvider);
        }

        [Unit]
        [Fact]
        public Task GetCloudConnectionForIdentityWithTokenTest()
        {
            IClientProvider clientProvider = GetMockDeviceClientProviderWithToken();
            return GetCloudConnectionTest(() => GetMockClientCredentialsWithToken(), clientProvider);
        }

        [Unit]
        [Fact]
        public void GetIsTokenExpiredTest()
        {
            // Arrange
            DateTime tokenExpiry = DateTime.UtcNow.AddYears(1);
            string token = TokenHelper.CreateSasToken("azure.devices.net", tokenExpiry);

            // Act
            TimeSpan expiryTimeRemaining = CloudConnection.GetTokenExpiryTimeRemaining("azure.devices.net", token);

            // Assert
            Assert.True(expiryTimeRemaining - (tokenExpiry - DateTime.UtcNow) < TimeSpan.FromSeconds(1));
        }

        [Unit]
        [Fact]
        public void GetTokenExpiryBufferSecondsTest()
        {
            string token = TokenHelper.CreateSasToken("azure.devices.net");
            TimeSpan timeRemaining = CloudConnection.GetTokenExpiryTimeRemaining("foo.azuredevices.net", token);
            Assert.True(timeRemaining > TimeSpan.Zero);
        }

        [Fact]
        [Unit]
        public async Task InitializeAndGetCloudProxyTest()
        {
            string iothubHostName = "test.azure-devices.net";
            string deviceId = "device1";

            IClientCredentials GetClientCredentialsWithNonExpiringToken()
            {
                string token = TokenHelper.CreateSasToken(iothubHostName, DateTime.UtcNow.AddMinutes(10));
                var identity = new DeviceIdentity(iothubHostName, deviceId);
                return new TokenCredentials(identity, token, string.Empty);
            }

            IClient client = GetMockDeviceClient();
            var deviceClientProvider = new Mock<IClientProvider>();
            deviceClientProvider.Setup(dc => dc.Create(It.IsAny<IIdentity>(), It.IsAny<IAuthenticationMethod>(), It.IsAny<ITransportSettings[]>()))
                .Returns(() => client);

            var transportSettings = new ITransportSettings[] { new AmqpTransportSettings(TransportType.Amqp_Tcp_Only) };

            var messageConverterProvider = new MessageConverterProvider(new Dictionary<Type, IMessageConverter> { [typeof(TwinCollection)] = Mock.Of<IMessageConverter>() });

            var cloudConnection = new CloudConnection((_, __) => { }, transportSettings, messageConverterProvider, deviceClientProvider.Object, Mock.Of<ICloudListener>(), TokenProvider, DeviceScopeIdentitiesCache, TimeSpan.FromMinutes(60), true);

            IClientCredentials clientCredentialsWithExpiringToken2 = GetClientCredentialsWithNonExpiringToken();
            ICloudProxy cloudProxy = await cloudConnection.CreateOrUpdateAsync(clientCredentialsWithExpiringToken2);

            // Wait for the task to complete
            await Task.Delay(TimeSpan.FromSeconds(10));
            Assert.Equal(cloudProxy, cloudConnection.CloudProxy.OrDefault());
            Assert.True(cloudProxy.IsActive);
            Mock.Get(client).Verify(c => c.OpenAsync(), Times.Once);
        }

        [Fact]
        [Unit]
        public async Task RefreshTokenTest()
        {
            string iothubHostName = "test.azure-devices.net";
            string deviceId = "device1";

            IClientCredentials GetClientCredentialsWithExpiringToken()
            {
                string token = TokenHelper.CreateSasToken(iothubHostName, DateTime.UtcNow.AddMinutes(3));
                var identity = new DeviceIdentity(iothubHostName, deviceId);
                return new TokenCredentials(identity, token, string.Empty);
            }

            IClientCredentials GetClientCredentialsWithNonExpiringToken()
            {
                string token = TokenHelper.CreateSasToken(iothubHostName, DateTime.UtcNow.AddMinutes(10));
                var identity = new DeviceIdentity(iothubHostName, deviceId);
                return new TokenCredentials(identity, token, string.Empty);
            }

            IAuthenticationMethod authenticationMethod = null;
            IClientProvider clientProvider = GetMockDeviceClientProviderWithToken((s, a, t) => authenticationMethod = a);

            var transportSettings = new ITransportSettings[] { new AmqpTransportSettings(TransportType.Amqp_Tcp_Only) };

            var receivedStatus = CloudConnectionStatus.ConnectionEstablished;
            void ConnectionStatusHandler(string id, CloudConnectionStatus status) => receivedStatus = status;
            var messageConverterProvider = new MessageConverterProvider(new Dictionary<Type, IMessageConverter> { [typeof(TwinCollection)] = Mock.Of<IMessageConverter>() });

            var cloudConnection = new CloudConnection(ConnectionStatusHandler, transportSettings, messageConverterProvider, clientProvider, Mock.Of<ICloudListener>(), TokenProvider, DeviceScopeIdentitiesCache, TimeSpan.FromMinutes(60), true);

            IClientCredentials clientCredentialsWithExpiringToken1 = GetClientCredentialsWithExpiringToken();
            ICloudProxy cloudProxy1 = await cloudConnection.CreateOrUpdateAsync(clientCredentialsWithExpiringToken1);
            Assert.True(cloudProxy1.IsActive);
            Assert.Equal(cloudProxy1, cloudConnection.CloudProxy.OrDefault());

            Assert.NotNull(authenticationMethod);
            var deviceAuthenticationWithTokenRefresh = authenticationMethod as DeviceAuthenticationWithTokenRefresh;
            Assert.NotNull(deviceAuthenticationWithTokenRefresh);

            Task<string> getTokenTask = deviceAuthenticationWithTokenRefresh.GetTokenAsync(iothubHostName);
            Assert.False(getTokenTask.IsCompleted);

            Assert.Equal(receivedStatus, CloudConnectionStatus.TokenNearExpiry);

            IClientCredentials clientCredentialsWithExpiringToken2 = GetClientCredentialsWithNonExpiringToken();
            ICloudProxy cloudProxy2 = await cloudConnection.CreateOrUpdateAsync(clientCredentialsWithExpiringToken2);

            // Wait for the task to complete
            await Task.Delay(TimeSpan.FromSeconds(10));

            Assert.True(getTokenTask.IsCompletedSuccessfully);
            Assert.Equal(cloudProxy2, cloudConnection.CloudProxy.OrDefault());
            Assert.True(cloudProxy2.IsActive);
            Assert.True(cloudProxy1.IsActive);
            Assert.Equal(cloudProxy1, cloudProxy2);
            Assert.Equal(getTokenTask.Result, (clientCredentialsWithExpiringToken2 as ITokenCredentials)?.Token);
        }

        [Fact]
        [Unit]
        public async Task RefreshTokenWithRetryTest()
        {
            string iothubHostName = "test.azure-devices.net";
            string deviceId = "device1";

            IClientCredentials GetClientCredentialsWithExpiringToken()
            {
                string token = TokenHelper.CreateSasToken(iothubHostName, DateTime.UtcNow.AddMinutes(3));
                var identity = new DeviceIdentity(iothubHostName, deviceId);
                return new TokenCredentials(identity, token, string.Empty);
            }

            IClientCredentials GetClientCredentialsWithNonExpiringToken()
            {
                string token = TokenHelper.CreateSasToken(iothubHostName, DateTime.UtcNow.AddMinutes(10));
                var identity = new DeviceIdentity(iothubHostName, deviceId);
                return new TokenCredentials(identity, token, string.Empty);
            }

            IAuthenticationMethod authenticationMethod = null;
            IClientProvider clientProvider = GetMockDeviceClientProviderWithToken((s, a, t) => authenticationMethod = a);

            var transportSettings = new ITransportSettings[] { new AmqpTransportSettings(TransportType.Amqp_Tcp_Only) };

            var receivedStatuses = new List<CloudConnectionStatus>();
            void ConnectionStatusHandler(string id, CloudConnectionStatus status) => receivedStatuses.Add(status);
            var messageConverterProvider = new MessageConverterProvider(new Dictionary<Type, IMessageConverter> { [typeof(TwinCollection)] = Mock.Of<IMessageConverter>() });

            var cloudConnection = new CloudConnection(ConnectionStatusHandler, transportSettings, messageConverterProvider, clientProvider, Mock.Of<ICloudListener>(), TokenProvider, DeviceScopeIdentitiesCache, TimeSpan.FromMinutes(60), true);

            IClientCredentials clientCredentialsWithExpiringToken1 = GetClientCredentialsWithExpiringToken();
            ICloudProxy cloudProxy1 = await cloudConnection.CreateOrUpdateAsync(clientCredentialsWithExpiringToken1);
            Assert.True(cloudProxy1.IsActive);
            Assert.Equal(cloudProxy1, cloudConnection.CloudProxy.OrDefault());

            Assert.NotNull(authenticationMethod);
            var deviceAuthenticationWithTokenRefresh = authenticationMethod as DeviceAuthenticationWithTokenRefresh;
            Assert.NotNull(deviceAuthenticationWithTokenRefresh);

            // Try to refresh token but get an expiring token
            Task<string> getTokenTask = deviceAuthenticationWithTokenRefresh.GetTokenAsync(iothubHostName);
            Assert.False(getTokenTask.IsCompleted);

            Assert.Equal(1, receivedStatuses.Count);
            Assert.Equal(receivedStatuses[0], CloudConnectionStatus.TokenNearExpiry);

            ICloudProxy cloudProxy2 = await cloudConnection.CreateOrUpdateAsync(clientCredentialsWithExpiringToken1);

            // Wait for the task to process
            await Task.Delay(TimeSpan.FromSeconds(5));

            Assert.False(getTokenTask.IsCompletedSuccessfully);
            Assert.Equal(cloudProxy2, cloudConnection.CloudProxy.OrDefault());
            Assert.True(cloudProxy2.IsActive);
            Assert.True(cloudProxy1.IsActive);
            Assert.Equal(cloudProxy1, cloudProxy2);

            // Wait for 20 secs for retry to happen
            await Task.Delay(TimeSpan.FromSeconds(20));

            // Check if retry happened
            Assert.Equal(2, receivedStatuses.Count);
            Assert.Equal(receivedStatuses[1], CloudConnectionStatus.TokenNearExpiry);

            IClientCredentials clientCredentialsWithNonExpiringToken = GetClientCredentialsWithNonExpiringToken();
            ICloudProxy cloudProxy3 = await cloudConnection.CreateOrUpdateAsync(clientCredentialsWithNonExpiringToken);

            // Wait for the task to complete
            await Task.Delay(TimeSpan.FromSeconds(5));

            Assert.True(getTokenTask.IsCompletedSuccessfully);
            Assert.Equal(cloudProxy3, cloudConnection.CloudProxy.OrDefault());
            Assert.True(cloudProxy3.IsActive);
            Assert.True(cloudProxy1.IsActive);
            Assert.Equal(cloudProxy1, cloudProxy3);
            Assert.Equal(getTokenTask.Result, (clientCredentialsWithNonExpiringToken as ITokenCredentials)?.Token);
        }

        [Unit]
        [Fact]
        public async Task UpdateDeviceConnectionTest()
        {
            int receivedConnectedStatusCount = 0;
            ConnectionStatusChangesHandler connectionStatusChangesHandler = null;
            string hostname = "dummy.azure-devices.net";
            string deviceId = "device1";

            IClientCredentials GetClientCredentials(TimeSpan tokenExpiryDuration)
            {
                string token = TokenHelper.CreateSasToken(hostname, DateTime.UtcNow.AddSeconds(tokenExpiryDuration.TotalSeconds));
                var identity = new DeviceIdentity(hostname, deviceId);
                return new TokenCredentials(identity, token, string.Empty);
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

            IAuthenticationMethod authenticationMethod = null;
            var deviceClientProvider = new Mock<IClientProvider>();
            deviceClientProvider.Setup(dc => dc.Create(It.IsAny<IIdentity>(), It.IsAny<IAuthenticationMethod>(), It.IsAny<ITransportSettings[]>()))
                .Callback<IIdentity, IAuthenticationMethod, ITransportSettings[]>((s, a, t) => authenticationMethod = a)
                .Returns(() => GetMockedDeviceClient());

            var messageConverterProvider = Mock.Of<IMessageConverterProvider>();

            ICloudConnectionProvider cloudConnectionProvider = new CloudConnectionProvider(messageConverterProvider, 1, deviceClientProvider.Object, Option.None<UpstreamProtocol>(), TokenProvider, DeviceScopeIdentitiesCache, TimeSpan.FromMinutes(60), true);
            cloudConnectionProvider.BindEdgeHub(Mock.Of<IEdgeHub>());
            var credentialsCache = Mock.Of<ICredentialsCache>();
            IConnectionManager connectionManager = new ConnectionManager(cloudConnectionProvider, credentialsCache);

            IClientCredentials clientCredentials1 = GetClientCredentials(TimeSpan.FromSeconds(10));
            Try<ICloudProxy> cloudProxyTry1 = await connectionManager.CreateCloudConnectionAsync(clientCredentials1);
            Assert.True(cloudProxyTry1.Success);

            IDeviceProxy deviceProxy1 = GetMockDeviceProxy();
            await connectionManager.AddDeviceConnection(clientCredentials1.Identity, deviceProxy1);

            await Task.Delay(TimeSpan.FromSeconds(10));
            Assert.NotNull(authenticationMethod);
            var deviceTokenRefresher = authenticationMethod as DeviceAuthenticationWithTokenRefresh;
            Assert.NotNull(deviceTokenRefresher);
            Task<string> tokenGetter = deviceTokenRefresher.GetTokenAsync(hostname);
            Assert.False(tokenGetter.IsCompleted);

            IClientCredentials clientCredentials2 = GetClientCredentials(TimeSpan.FromMinutes(2));
            Try<ICloudProxy> cloudProxyTry2 = await connectionManager.CreateCloudConnectionAsync(clientCredentials2);
            Assert.True(cloudProxyTry2.Success);

            IDeviceProxy deviceProxy2 = GetMockDeviceProxy();
            await connectionManager.AddDeviceConnection(clientCredentials2.Identity, deviceProxy2);

            await Task.Delay(TimeSpan.FromSeconds(3));
            Assert.False(tokenGetter.IsCompleted);

            IClientCredentials clientCredentials3 = GetClientCredentials(TimeSpan.FromMinutes(10));
            Try<ICloudProxy> cloudProxyTry3 = await connectionManager.CreateCloudConnectionAsync(clientCredentials3);
            Assert.True(cloudProxyTry3.Success);

            IDeviceProxy deviceProxy3 = GetMockDeviceProxy();
            await connectionManager.AddDeviceConnection(clientCredentials3.Identity, deviceProxy3);

            await Task.Delay(TimeSpan.FromSeconds(23));
            Assert.True(tokenGetter.IsCompleted);
            Assert.Equal(tokenGetter.Result, (clientCredentials3 as ITokenCredentials)?.Token);
        }

        [Unit]
        [Fact]
        public async Task UpdateInvalidIdentityWithTokenTest()
        {
            var deviceClientProvider = new Mock<IClientProvider>();
            deviceClientProvider.SetupSequence(dc => dc.Create(It.IsAny<IIdentity>(), It.IsAny<IAuthenticationMethod>(), It.IsAny<ITransportSettings[]>()))
                .Returns(GetMockDeviceClient())
                .Throws(new UnauthorizedException("Unauthorized"));

            var transportSettings = new ITransportSettings[] { new AmqpTransportSettings(TransportType.Amqp_Tcp_Only) };

            var messageConverterProvider = new MessageConverterProvider(new Dictionary<Type, IMessageConverter> { [typeof(TwinCollection)] = Mock.Of<IMessageConverter>() });
            var cloudConnection = new CloudConnection((_, __) => { }, transportSettings, messageConverterProvider, deviceClientProvider.Object, Mock.Of<ICloudListener>(), TokenProvider, DeviceScopeIdentitiesCache, TimeSpan.FromMinutes(60), true);

            IClientCredentials identity1 = GetMockClientCredentialsWithToken();
            ICloudProxy cloudProxy1 = await cloudConnection.CreateOrUpdateAsync(identity1);
            Assert.True(cloudProxy1.IsActive);
            Assert.Equal(cloudProxy1, cloudConnection.CloudProxy.OrDefault());

            IClientCredentials identity2 = GetMockClientCredentialsWithToken();
            await Assert.ThrowsAsync<UnauthorizedException>(() => cloudConnection.CreateOrUpdateAsync(identity2));
            Assert.True(cloudProxy1.IsActive);
            Assert.Equal(cloudProxy1, cloudConnection.CloudProxy.OrDefault());
        }

        static async Task GetCloudConnectionTest(Func<IClientCredentials> credentialsGenerator, IClientProvider clientProvider)
        {
            var transportSettings = new ITransportSettings[] { new AmqpTransportSettings(TransportType.Amqp_Tcp_Only) };
            var messageConverterProvider = new MessageConverterProvider(new Dictionary<Type, IMessageConverter> { [typeof(TwinCollection)] = Mock.Of<IMessageConverter>() });
            var cloudConnection = new CloudConnection((_, __) => { }, transportSettings, messageConverterProvider, clientProvider, Mock.Of<ICloudListener>(), TokenProvider, DeviceScopeIdentitiesCache, TimeSpan.FromMinutes(60), true);

            IClientCredentials clientCredentials1 = credentialsGenerator();
            ICloudProxy cloudProxy1 = await cloudConnection.CreateOrUpdateAsync(clientCredentials1);
            Assert.True(cloudProxy1.IsActive);
            Assert.Equal(cloudProxy1, cloudConnection.CloudProxy.OrDefault());

            IClientCredentials clientCredentials2 = credentialsGenerator();
            ICloudProxy cloudProxy2 = await cloudConnection.CreateOrUpdateAsync(clientCredentials2);
            Assert.Equal(cloudProxy2, cloudConnection.CloudProxy.OrDefault());
            Assert.True(cloudProxy2.IsActive);
            Assert.False(cloudProxy1.IsActive);
            Assert.NotEqual(cloudProxy1, cloudProxy2);
        }

        static IClientCredentials GetMockClientCredentialsWithKey(string hostname = "dummy.azure-devices.net", string deviceId = "device1")
        {
            var identity = new DeviceIdentity(hostname, deviceId);
            return new SharedKeyCredentials(identity, "dummyConnStr", string.Empty);
        }

        static IClientCredentials GetMockClientCredentialsWithToken(
            string hostname = "dummy.azure-devices.net",
            string deviceId = "device1")
        {
            string token = TokenHelper.CreateSasToken(hostname);
            var identity = new DeviceIdentity(hostname, deviceId);
            return new TokenCredentials(identity, token, string.Empty);
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

        static IClientProvider GetMockDeviceClientProviderWithKey()
        {
            var deviceClientProvider = new Mock<IClientProvider>();
            deviceClientProvider.Setup(dc => dc.Create(It.IsAny<IIdentity>(), It.IsAny<string>(), It.IsAny<ITransportSettings[]>()))
                .Returns(() => GetMockDeviceClient());
            return deviceClientProvider.Object;
        }

        static IClientProvider GetMockDeviceClientProviderWithToken(Action<IIdentity, IAuthenticationMethod, ITransportSettings[]> callback = null)
        {
            var deviceClientProvider = new Mock<IClientProvider>();
            deviceClientProvider.Setup(dc => dc.Create(It.IsAny<IIdentity>(), It.IsAny<IAuthenticationMethod>(), It.IsAny<ITransportSettings[]>()))
                .Callback<IIdentity, IAuthenticationMethod, ITransportSettings[]>((c, a, t) => callback?.Invoke(c, a, t))
                .Returns(() => GetMockDeviceClient());
            return deviceClientProvider.Object;
        }
    }
}
