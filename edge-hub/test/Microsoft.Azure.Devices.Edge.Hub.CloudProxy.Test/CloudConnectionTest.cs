// Copyright (c) Microsoft. All rights reserved.

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
    using Moq;
    using Xunit;

    public class CloudConnectionTest
    {
        [Unit]
        [Fact]
        public void GetTokenExpiryBufferSecondsTest()
        {
            string token = TokenHelper.CreateSasToken("azure.devices.net");
            TimeSpan timeRemaining = CloudConnection.GetTokenExpiryTimeRemaining("foo.azuredevices.net", token);
            Assert.True(timeRemaining > TimeSpan.Zero);
        }

        [Unit]
        [Fact]
        public Task GetCloudConnectionForIdentityWithTokenTest()
        {
            IDeviceClientProvider deviceClientProvider = GetMockDeviceClientProviderWithToken();
            return GetCloudConnectionTest(() => GetMockIdentityWithToken(), deviceClientProvider);
        }

        [Unit]
        [Fact]
        public Task GetCloudConnectionForIdentityWithKeyTest()
        {
            IDeviceClientProvider deviceClientProvider = GetMockDeviceClientProviderWithKey();
            return GetCloudConnectionTest(() => GetMockIdentityWithKey(), deviceClientProvider);
        }

        [Unit]
        [Fact]
        public async Task UpdateInvalidIdentityWithTokenTest()
        {
            var deviceClientProvider = new Mock<IDeviceClientProvider>();
            deviceClientProvider.SetupSequence(dc => dc.Create(It.IsAny<string>(), It.IsAny<IAuthenticationMethod>(), It.IsAny<ITransportSettings[]>()))
                .Returns(GetMockDeviceClient())
                .Throws(new UnauthorizedException("Unauthorized"));

            var transportSettings = new ITransportSettings[] { new AmqpTransportSettings(TransportType.Amqp_Tcp_Only) };

            var messageConverterProvider = new MessageConverterProvider(new Dictionary<Type, IMessageConverter>());

            var cloudConnection = new CloudConnection((_, __) => { }, transportSettings, messageConverterProvider, deviceClientProvider.Object);

            IIdentity identity1 = GetMockIdentityWithToken();
            ICloudProxy cloudProxy1 = await cloudConnection.CreateOrUpdateAsync(identity1);
            Assert.True(cloudProxy1.IsActive);
            Assert.Equal(cloudProxy1, cloudConnection.CloudProxy.OrDefault());

            IIdentity identity2 = GetMockIdentityWithToken();
            await Assert.ThrowsAsync<AggregateException>(() => cloudConnection.CreateOrUpdateAsync(identity2));
            Assert.True(cloudProxy1.IsActive);
            Assert.Equal(cloudProxy1, cloudConnection.CloudProxy.OrDefault());
        }

        [Fact]
        [Unit]
        public async Task RefreshTokenTest()
        {
            string iothubHostName = "test.azure-devices.net";
            string deviceId = "device1";

            IIdentity GetIdentityWithExpiringToken()
            {
                string token = TokenHelper.CreateSasToken(iothubHostName, DateTime.UtcNow.AddSeconds(10));
                var identity = Mock.Of<Core.Device.IDeviceIdentity>(
                    i => i.Token == Option.Some(token)
                        && i.IotHubHostName == iothubHostName
                        && i.Id == deviceId
                        && i.DeviceId == deviceId);
                return identity;
            }

            IAuthenticationMethod authenticationMethod = null;
            IDeviceClientProvider deviceClientProvider = GetMockDeviceClientProviderWithToken((s, a, t) => authenticationMethod = a);

            var transportSettings = new ITransportSettings[] { new AmqpTransportSettings(TransportType.Amqp_Tcp_Only) };

            var receivedStatus = CloudConnectionStatus.ConnectionEstablished;
            void ConnectionStatusHandler(string id, CloudConnectionStatus status) => receivedStatus = status;
            var messageConverterProvider = new MessageConverterProvider(new Dictionary<Type, IMessageConverter>());

            var cloudConnection = new CloudConnection(ConnectionStatusHandler, transportSettings, messageConverterProvider, deviceClientProvider);

            IIdentity identity1 = GetIdentityWithExpiringToken();
            ICloudProxy cloudProxy1 = await cloudConnection.CreateOrUpdateAsync(identity1);
            Assert.True(cloudProxy1.IsActive);
            Assert.Equal(cloudProxy1, cloudConnection.CloudProxy.OrDefault());

            Assert.NotNull(authenticationMethod);
            var deviceAuthenticationWithTokenRefresh = authenticationMethod as DeviceAuthenticationWithTokenRefresh;
            Assert.NotNull(deviceAuthenticationWithTokenRefresh);

            // Wait for the token to expire
            await Task.Delay(TimeSpan.FromSeconds(10));

            Task<string> getTokenTask = deviceAuthenticationWithTokenRefresh.GetTokenAsync(iothubHostName);
            Assert.False(getTokenTask.IsCompleted);

            Assert.Equal(receivedStatus, CloudConnectionStatus.TokenNearExpiry);

            IIdentity identity2 = GetIdentityWithExpiringToken();
            ICloudProxy cloudProxy2 = await cloudConnection.CreateOrUpdateAsync(identity2);

            // Wait for the task to complete
            await Task.Delay(TimeSpan.FromSeconds(10));

            Assert.Equal(cloudProxy2, cloudConnection.CloudProxy.OrDefault());
            Assert.True(cloudProxy2.IsActive);
            Assert.True(cloudProxy1.IsActive);
            Assert.Equal(cloudProxy1, cloudProxy2);
            Assert.True(getTokenTask.IsCompletedSuccessfully);
            Assert.Equal(getTokenTask.Result, identity2.Token.OrDefault());
        }

        [Fact]
        [Unit]
        public async Task CloudConnectionCallbackTest()
        {
            int receivedConnectedStatusCount = 0;
            ConnectionStatusChangesHandler connectionStatusChangesHandler = (_, __) => { };

            IDeviceClient GetMockedDeviceClient()
            {
                var deviceClient = new Mock<IDeviceClient>();
                deviceClient.SetupGet(dc => dc.IsActive).Returns(true);
                deviceClient.Setup(dc => dc.CloseAsync())
                    .Callback(() => deviceClient.SetupGet(dc => dc.IsActive).Returns(false))
                    .Returns(Task.FromResult(true));

                deviceClient.Setup(dc => dc.SetConnectionStatusChangesHandler(It.IsAny<ConnectionStatusChangesHandler>()))
                    .Callback<ConnectionStatusChangesHandler>(c => connectionStatusChangesHandler = c);

                deviceClient.Setup(dc => dc.OpenAsync())
                    .Callback(() =>
                    {
                        int currentCount = receivedConnectedStatusCount;
                        Assert.NotNull(connectionStatusChangesHandler);
                        connectionStatusChangesHandler.Invoke(ConnectionStatus.Connected, ConnectionStatusChangeReason.Connection_Ok);
                        Assert.Equal(receivedConnectedStatusCount, currentCount);
                    })
                    .Returns(Task.CompletedTask);
                return deviceClient.Object;
            }

            var deviceClientProvider = new Mock<IDeviceClientProvider>();
            deviceClientProvider.Setup(dc => dc.Create(It.IsAny<string>(), It.IsAny<IAuthenticationMethod>(), It.IsAny<ITransportSettings[]>()))
                .Returns(() => GetMockedDeviceClient());

            var transportSettings = new ITransportSettings[] { new AmqpTransportSettings(TransportType.Amqp_Tcp_Only) };

            void ConnectionStatusHandler(string id, CloudConnectionStatus status)
            {
                if (status == CloudConnectionStatus.ConnectionEstablished)
                {
                    receivedConnectedStatusCount++;
                }
            }

            var messageConverterProvider = new MessageConverterProvider(new Dictionary<Type, IMessageConverter>());

            var cloudConnection = new CloudConnection(ConnectionStatusHandler, transportSettings, messageConverterProvider, deviceClientProvider.Object);

            IIdentity identity1 = GetMockIdentityWithToken();
            Assert.Equal(receivedConnectedStatusCount, 0);
            ICloudProxy cloudProxy1 = await cloudConnection.CreateOrUpdateAsync(identity1);
            Assert.True(cloudProxy1.IsActive);
            Assert.Equal(cloudProxy1, cloudConnection.CloudProxy.OrDefault());
            Assert.Equal(receivedConnectedStatusCount, 1);

            Assert.NotNull(connectionStatusChangesHandler);
            connectionStatusChangesHandler.Invoke(ConnectionStatus.Connected, ConnectionStatusChangeReason.Connection_Ok);
            Assert.Equal(receivedConnectedStatusCount, 2);

            IIdentity identity2 = GetMockIdentityWithToken();
            ICloudProxy cloudProxy2 = await cloudConnection.CreateOrUpdateAsync(identity2);
            Assert.True(cloudProxy2.IsActive);
            Assert.Equal(cloudProxy2, cloudConnection.CloudProxy.OrDefault());
            Assert.Equal(receivedConnectedStatusCount, 3);

            connectionStatusChangesHandler.Invoke(ConnectionStatus.Connected, ConnectionStatusChangeReason.Connection_Ok);
            Assert.Equal(receivedConnectedStatusCount, 4);
        }

        [Unit]
        [Fact]
        public async Task UpdateDeviceConnectionTest()
        {
            int receivedConnectedStatusCount = 0;
            ConnectionStatusChangesHandler connectionStatusChangesHandler = null;
            string hostname = "dummy.azure-devices.net";
            string deviceId = "device1";

            IIdentity GetMockIdentityWithToken()
            {
                string token = TokenHelper.CreateSasToken(hostname, DateTime.UtcNow.AddSeconds(10));
                var identity = Mock.Of<IDeviceIdentity>(i => i.Token == Option.Some(token)
                    && i.IotHubHostName == hostname
                    && i.Id == deviceId
                    && i.DeviceId == deviceId);
                return identity;
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

            IDeviceClient GetMockedDeviceClient()
            {
                var deviceClient = new Mock<IDeviceClient>();
                deviceClient.SetupGet(dc => dc.IsActive).Returns(true);
                deviceClient.Setup(dc => dc.CloseAsync())
                    .Callback(() => deviceClient.SetupGet(dc => dc.IsActive).Returns(false))
                    .Returns(Task.FromResult(true));

                deviceClient.Setup(dc => dc.SetConnectionStatusChangesHandler(It.IsAny<ConnectionStatusChangesHandler>()))
                    .Callback<ConnectionStatusChangesHandler>(c => connectionStatusChangesHandler = c);

                deviceClient.Setup(dc => dc.OpenAsync())
                    .Callback(() =>
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
            var deviceClientProvider = new Mock<IDeviceClientProvider>();
            deviceClientProvider.Setup(dc => dc.Create(It.IsAny<string>(), It.IsAny<IAuthenticationMethod>(), It.IsAny<ITransportSettings[]>()))
                .Callback<string, IAuthenticationMethod, ITransportSettings[]>((s, a, t) => authenticationMethod = a)
                .Returns(() => GetMockedDeviceClient());

            var messageConverterProvider = Mock.Of<IMessageConverterProvider>();

            ICloudConnectionProvider cloudConnectionProvider = new CloudConnectionProvider(messageConverterProvider, 1, deviceClientProvider.Object);
            IConnectionManager connectionManager = new ConnectionManager(cloudConnectionProvider);

            IIdentity identity1 = GetMockIdentityWithToken();
            Try<ICloudProxy> cloudProxyTry1 = await connectionManager.CreateCloudConnectionAsync(identity1);
            Assert.True(cloudProxyTry1.Success);

            IDeviceProxy deviceProxy1 = GetMockDeviceProxy();
            await connectionManager.AddDeviceConnection(identity1, deviceProxy1);

            await Task.Delay(TimeSpan.FromSeconds(10));
            Assert.NotNull(authenticationMethod);
            var deviceTokenRefresher = authenticationMethod as DeviceAuthenticationWithTokenRefresh;
            Assert.NotNull(deviceTokenRefresher);
            Task<string> tokenGetter = deviceTokenRefresher.GetTokenAsync(hostname);
            Assert.False(tokenGetter.IsCompleted);

            IIdentity identity2 = GetMockIdentityWithToken();
            Try<ICloudProxy> cloudProxyTry2 = await connectionManager.CreateCloudConnectionAsync(identity2);
            Assert.True(cloudProxyTry2.Success);

            IDeviceProxy deviceProxy2 = GetMockDeviceProxy();
            await connectionManager.AddDeviceConnection(identity2, deviceProxy2);

            await Task.Delay(TimeSpan.FromSeconds(3));
            Assert.True(tokenGetter.IsCompleted);
            Assert.Equal(tokenGetter.Result, identity2.Token.OrDefault());

            await Task.Delay(TimeSpan.FromSeconds(10));
            Assert.NotNull(authenticationMethod);
            deviceTokenRefresher = authenticationMethod as DeviceAuthenticationWithTokenRefresh;
            Assert.NotNull(deviceTokenRefresher);
            tokenGetter = deviceTokenRefresher.GetTokenAsync(hostname);
            Assert.False(tokenGetter.IsCompleted);

            IIdentity identity3 = GetMockIdentityWithToken();
            Try<ICloudProxy> cloudProxyTry3 = await connectionManager.CreateCloudConnectionAsync(identity3);
            Assert.True(cloudProxyTry3.Success);

            IDeviceProxy deviceProxy3 = GetMockDeviceProxy();
            await connectionManager.AddDeviceConnection(identity3, deviceProxy3);

            await Task.Delay(TimeSpan.FromSeconds(3));
            Assert.True(tokenGetter.IsCompleted);
            Assert.Equal(tokenGetter.Result, identity3.Token.OrDefault());

            Mock.VerifyAll(Mock.Get(deviceProxy1), Mock.Get(deviceProxy2));
        }

        static async Task GetCloudConnectionTest(Func<IIdentity> identityGenerator, IDeviceClientProvider deviceClientProvider)
        {
            var transportSettings = new ITransportSettings[] { new AmqpTransportSettings(TransportType.Amqp_Tcp_Only) };
            var messageConverterProvider = new MessageConverterProvider(new Dictionary<Type, IMessageConverter>());

            var cloudConnection = new CloudConnection((_, __) => { }, transportSettings, messageConverterProvider, deviceClientProvider);

            IIdentity identity1 = identityGenerator();
            ICloudProxy cloudProxy1 = await cloudConnection.CreateOrUpdateAsync(identity1);
            Assert.True(cloudProxy1.IsActive);
            Assert.Equal(cloudProxy1, cloudConnection.CloudProxy.OrDefault());

            IIdentity identity2 = identityGenerator();
            ICloudProxy cloudProxy2 = await cloudConnection.CreateOrUpdateAsync(identity2);
            Assert.Equal(cloudProxy2, cloudConnection.CloudProxy.OrDefault());
            Assert.True(cloudProxy2.IsActive);
            Assert.False(cloudProxy1.IsActive);
            Assert.NotEqual(cloudProxy1, cloudProxy2);
        }

        static IDeviceClientProvider GetMockDeviceClientProviderWithKey()
        {
            var deviceClientProvider = new Mock<IDeviceClientProvider>();
            deviceClientProvider.Setup(dc => dc.Create(It.IsAny<string>(), It.IsAny<ITransportSettings[]>()))
                .Returns(() => GetMockDeviceClient());
            return deviceClientProvider.Object;
        }

        static IDeviceClientProvider GetMockDeviceClientProviderWithToken(Action<string, IAuthenticationMethod, ITransportSettings[]> callback = null)
        {
            var deviceClientProvider = new Mock<IDeviceClientProvider>();
            deviceClientProvider.Setup(dc => dc.Create(It.IsAny<string>(), It.IsAny<IAuthenticationMethod>(), It.IsAny<ITransportSettings[]>()))
                .Callback<string, IAuthenticationMethod, ITransportSettings[]>((c, a, t) => callback?.Invoke(c, a, t))
                .Returns(() => GetMockDeviceClient());
            return deviceClientProvider.Object;
        }

        static IDeviceClient GetMockDeviceClient()
        {
            var deviceClient = new Mock<IDeviceClient>();
            deviceClient.SetupGet(dc => dc.IsActive).Returns(true);
            deviceClient.Setup(dc => dc.CloseAsync())
                .Callback(() => deviceClient.SetupGet(dc => dc.IsActive).Returns(false))
                .Returns(Task.FromResult(true));
            return deviceClient.Object;
        }

        static IIdentity GetMockIdentityWithKey(string hostname = "dummy.azure-devices.net", string deviceId = "device1")
        {
            var identity = Mock.Of<Core.Device.IDeviceIdentity>(i => i.Token == Option.None<string>()
                && i.ConnectionString == "dummyConnStr"
                && i.IotHubHostName == hostname
                && i.Id == deviceId
                && i.DeviceId == deviceId);
            return identity;
        }

        static IIdentity GetMockIdentityWithToken(string hostname = "dummy.azure-devices.net",
            string deviceId = "device1")
        {
            string token = TokenHelper.CreateSasToken(hostname);
            var identity = Mock.Of<Core.Device.IDeviceIdentity>(i => i.Token == Option.Some(token)
                && i.IotHubHostName == hostname
                && i.Id == deviceId
                && i.DeviceId == deviceId);
            return identity;
        }
    }
}
