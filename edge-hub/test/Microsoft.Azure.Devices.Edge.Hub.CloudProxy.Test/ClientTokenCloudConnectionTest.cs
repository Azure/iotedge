// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy.Test
{
    using System;
    using System.Collections.Generic;
    using System.Security.Authentication;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Exceptions;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Shared;
    using Moq;
    using Xunit;
    using static Microsoft.Azure.Devices.Edge.Hub.CloudProxy.ClientTokenCloudConnection;
    using TransportType = Client.TransportType;

    [Unit]
    public class ClientTokenCloudConnectionTest
    {
        const string DummyProductInfo = "IoTEdge 1.0.6 1.20.0-RC2";
        static readonly ICredentialsCache CredentialCache = Mock.Of<ICredentialsCache>();

        [Unit]
        [Fact]
        public async Task GetCloudConnectionForIdentityWithTokenTest()
        {
            var client = GetMockDeviceClient();
            var deviceClientProvider = new Mock<IClientProvider>();
            deviceClientProvider.Setup(dc => dc.Create(It.IsAny<IIdentity>(), It.IsAny<ITokenProvider>(), It.IsAny<ITransportSettings[]>(), Option.None<string>()))
                .Returns(() => client);
            var transportSettings = new ITransportSettings[] { new AmqpTransportSettings(TransportType.Amqp_Tcp_Only) };
            var messageConverterProvider = new MessageConverterProvider(new Dictionary<Type, IMessageConverter> { [typeof(TwinCollection)] = Mock.Of<IMessageConverter>() });
            var tokenCredentials = GetMockClientCredentialsWithToken();
            var cloudConnection = await Create(
                tokenCredentials.Identity,
                CredentialCache,
                (_, __) => { },
                transportSettings,
                messageConverterProvider,
                deviceClientProvider.Object,
                Mock.Of<ICloudListener>(),
                TimeSpan.FromMinutes(60),
                true,
                TimeSpan.FromSeconds(20),
                DummyProductInfo,
                Option.None<string>(),
                Option.Some(tokenCredentials.Token));
            Option<ICloudProxy> cloudProxy1 = cloudConnection.CloudProxy;

            Assert.True(cloudProxy1.HasValue);
            Assert.True(cloudProxy1.OrDefault().IsActive);
            Mock.Get(client).Verify(c => c.OpenAsync(), Times.Once);

            var updatedTokenCredentials = GetMockClientCredentialsWithToken();
            ICloudProxy cloudProxy2 = await cloudConnection.UpdateTokenAsync(updatedTokenCredentials);

            Assert.Equal(cloudProxy2, cloudConnection.CloudProxy.OrDefault());
            Assert.True(cloudProxy2.IsActive);
            Assert.Same(cloudProxy1.OrDefault(), cloudProxy2);
            Mock.Get(client).Verify(c => c.OpenAsync(), Times.Once);
        }

        [Unit]
        [Fact]
        public async Task UpdateInvalidIdentityWithTokenTest()
        {
            var deviceClientProvider = new Mock<IClientProvider>();
            deviceClientProvider.Setup(dc => dc.Create(It.IsAny<IIdentity>(), It.IsAny<ITokenProvider>(), It.IsAny<ITransportSettings[]>(), Option.None<string>()))
                .Throws(new UnauthorizedException("Unauthorized"));
            var transportSettings = new ITransportSettings[] { new AmqpTransportSettings(TransportType.Amqp_Tcp_Only) };
            var messageConverterProvider = new MessageConverterProvider(new Dictionary<Type, IMessageConverter> { [typeof(TwinCollection)] = Mock.Of<IMessageConverter>() });
            var tokenCredentials = GetMockClientCredentialsWithToken();

            await Assert.ThrowsAsync<UnauthorizedException>(() => Create(
                tokenCredentials.Identity,
                CredentialCache,
                (_, __) => { },
                transportSettings,
                messageConverterProvider,
                deviceClientProvider.Object,
                Mock.Of<ICloudListener>(),
                TimeSpan.FromMinutes(60),
                true,
                TimeSpan.FromSeconds(20),
                DummyProductInfo,
                Option.None<string>(),
                Option.Some(tokenCredentials.Token)));
        }

        [Fact]
        [Unit]
        public async Task RefreshTokenTest()
        {
            var validTokenCredentials = GetMockClientCredentialsWithToken();
            var identity = validTokenCredentials.Identity;
            var expiringToken = TokenHelper.CreateSasToken(validTokenCredentials.Identity.IotHubHostName, DateTime.UtcNow.AddMinutes(3));

            Mock.Get(CredentialCache)
                .Setup(cc => cc.Get(It.Is<IIdentity>(id => id == identity)))
                .Returns(Task.FromResult(Option.Some<IClientCredentials>(validTokenCredentials)));

            ITokenProvider tokenProvider = null;
            var client = new Mock<IClient>();
            var deviceClientProvider = new Mock<IClientProvider>();
            deviceClientProvider.Setup(dc => dc.Create(It.IsAny<IIdentity>(), It.IsAny<ITokenProvider>(), It.IsAny<ITransportSettings[]>(), Option.None<string>()))
                .Callback<IIdentity, ITokenProvider, ITransportSettings[], Option<string>>((id, tp, ts, mid) => tokenProvider = tp)
                .Returns(() => client.Object);
            client.Setup(c => c.IsActive)
                .Returns(true);
            client.Setup(c => c.OpenAsync())
                .Callback(async () => await tokenProvider.GetTokenAsync(Option.None<TimeSpan>()))
                .Returns(Task.CompletedTask);
            var transportSettings = new ITransportSettings[] { new AmqpTransportSettings(TransportType.Amqp_Tcp_Only) };
            var messageConverterProvider = new MessageConverterProvider(new Dictionary<Type, IMessageConverter> { [typeof(TwinCollection)] = Mock.Of<IMessageConverter>() });

            var cloudConnection = await Create(
                identity,
                CredentialCache,
                (_, __) => { },
                transportSettings,
                messageConverterProvider,
                deviceClientProvider.Object,
                Mock.Of<ICloudListener>(),
                TimeSpan.FromMinutes(60),
                true,
                TimeSpan.FromSeconds(20),
                DummyProductInfo,
                Option.None<string>(),
                Option.Some(expiringToken));

            Assert.NotNull(tokenProvider);
            Option<ICloudProxy> cloudProxy = cloudConnection.CloudProxy;
            Assert.True(cloudProxy.HasValue);
            Assert.True(cloudProxy.OrDefault().IsActive);

            Mock.Get(CredentialCache).Verify(cc => cc.Get(It.Is<IIdentity>(id => id == identity)), Times.Once);
        }

        [Fact]
        [Unit]
        public async Task RefreshTokenWithRetryTest()
        {
            var validTokenCredentials = GetMockClientCredentialsWithToken();
            var expiringTokenCredentials = GetMockClientCredentialsWithToken(expired: true);
            var identity = validTokenCredentials.Identity;
            var expiringToken = expiringTokenCredentials.Token;

            Mock.Get(CredentialCache)
                .SetupSequence(cc => cc.Get(It.Is<IIdentity>(id => id == identity)))
                .Returns(Task.FromResult(Option.Some<IClientCredentials>(expiringTokenCredentials)))
                .Returns(Task.FromResult(Option.None<IClientCredentials>()))
                .Returns(Task.FromResult(Option.Some<IClientCredentials>(validTokenCredentials)));

            ITokenProvider tokenProvider = null;
            var deviceClientProvider = new Mock<IClientProvider>();
            var client = new Mock<IClient>();
            client.Setup(c => c.IsActive)
                .Returns(true);
            deviceClientProvider.Setup(dc => dc.Create(It.IsAny<IIdentity>(), It.IsAny<ITokenProvider>(), It.IsAny<ITransportSettings[]>(), Option.None<string>()))
                .Callback<IIdentity, ITokenProvider, ITransportSettings[], Option<string>>((id, tp, ts, mid) => tokenProvider = tp)
                .Returns(() => client.Object);
            client.Setup(c => c.OpenAsync())
                .Returns(async () => await PollForTokenAsync(tokenProvider, Option.Some(TimeSpan.FromSeconds(10))));
            var transportSettings = new ITransportSettings[] { new AmqpTransportSettings(TransportType.Amqp_Tcp_Only) };
            var messageConverterProvider = new MessageConverterProvider(new Dictionary<Type, IMessageConverter> { [typeof(TwinCollection)] = Mock.Of<IMessageConverter>() });

            var cloudConnection = await Create(
                identity,
                CredentialCache,
                (_, __) => { },
                transportSettings,
                messageConverterProvider,
                deviceClientProvider.Object,
                Mock.Of<ICloudListener>(),
                TimeSpan.FromMinutes(60),
                true,
                TimeSpan.FromSeconds(20),
                DummyProductInfo,
                Option.None<string>(),
                Option.Some(expiringToken));

            Assert.NotNull(tokenProvider);
            Option<ICloudProxy> cloudProxy = cloudConnection.CloudProxy;
            Assert.True(cloudProxy.HasValue);
            Assert.True(cloudProxy.OrDefault().IsActive);
            Mock.Get(CredentialCache).Verify(cc => cc.Get(It.Is<IIdentity>(id => id == identity)), Times.Exactly(3));
        }

        [Fact]
        [Unit]
        public async Task RefreshTokenFailureTest()
        {
            var validTokenCredentials = GetMockClientCredentialsWithToken();
            var expiringTokenCredentials = GetMockClientCredentialsWithToken(expired: true);
            var identity = validTokenCredentials.Identity;
            var expiringToken = expiringTokenCredentials.Token;

            Mock.Get(CredentialCache)
                .Setup(cc => cc.Get(It.Is<IIdentity>(id => id == identity)))
                .Returns(Task.FromResult(Option.Some<IClientCredentials>(expiringTokenCredentials)));

            ITokenProvider tokenProvider = null;
            var deviceClientProvider = new Mock<IClientProvider>();
            var client = new Mock<IClient>();
            client.Setup(c => c.IsActive)
                .Returns(true);
            deviceClientProvider.Setup(dc => dc.Create(It.IsAny<IIdentity>(), It.IsAny<ITokenProvider>(), It.IsAny<ITransportSettings[]>(), Option.None<string>()))
                .Callback<IIdentity, ITokenProvider, ITransportSettings[], Option<string>>((id, tp, ts, mid) => tokenProvider = tp)
                .Returns(() => client.Object);
            client.Setup(c => c.OpenAsync())
                .Returns(async () => await PollForTokenAsync(tokenProvider, Option.Some(TimeSpan.FromSeconds(10))));
            var transportSettings = new ITransportSettings[] { new AmqpTransportSettings(TransportType.Amqp_Tcp_Only) };
            var messageConverterProvider = new MessageConverterProvider(new Dictionary<Type, IMessageConverter> { [typeof(TwinCollection)] = Mock.Of<IMessageConverter>() });

            await Assert.ThrowsAsync<AuthenticationException>(() => Create(
                expiringTokenCredentials.Identity,
                CredentialCache,
                (_, __) => { },
                transportSettings,
                messageConverterProvider,
                deviceClientProvider.Object,
                Mock.Of<ICloudListener>(),
                TimeSpan.FromMinutes(60),
                true,
                TimeSpan.FromSeconds(20),
                DummyProductInfo,
                Option.None<string>(),
                Option.Some(expiringTokenCredentials.Token)));
        }

        static async Task PollForTokenAsync(ITokenProvider tokenProvider, Option<TimeSpan> pollInterval, int times = 5)
        {
            var interval = pollInterval.GetOrElse(() => TimeSpan.FromMilliseconds(10));
            Exception e = null;
            for (int i = 0; i < times; i++)
            {
                try
                {
                    await tokenProvider.GetTokenAsync(Option.None<TimeSpan>());
                    return;
                }
                catch (TimeoutException ex)
                {
                    e = ex;
                    await Task.Delay(interval);
                }
            }

            throw e;
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
            string deviceId = "device1",
            bool expired = false)
        {
            string token = TokenHelper.CreateSasToken(hostname, expired: expired);
            var identity = new DeviceIdentity(hostname, deviceId);
            return new TokenCredentials(identity, token, string.Empty, Option.None<string>(), false);
        }
    }
}
