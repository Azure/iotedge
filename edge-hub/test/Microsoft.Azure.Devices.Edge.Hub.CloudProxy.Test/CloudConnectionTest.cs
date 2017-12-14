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

            var cloudConnection = new CloudConnection(_ => { }, transportSettings, messageConverterProvider, deviceClientProvider.Object);

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
            void ConnectionStatusHandler(CloudConnectionStatus status) => receivedStatus = status;
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

        static async Task GetCloudConnectionTest(Func<IIdentity> identityGenerator, IDeviceClientProvider deviceClientProvider)
        {
            var transportSettings = new ITransportSettings[] { new AmqpTransportSettings(TransportType.Amqp_Tcp_Only) };
            var messageConverterProvider = new MessageConverterProvider(new Dictionary<Type, IMessageConverter>());

            var cloudConnection = new CloudConnection(_ => { }, transportSettings, messageConverterProvider, deviceClientProvider);

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
