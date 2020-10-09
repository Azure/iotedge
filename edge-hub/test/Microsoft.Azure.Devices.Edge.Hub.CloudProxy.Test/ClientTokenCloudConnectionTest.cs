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
                tokenCredentials,
                (_, __) => { },
                transportSettings,
                messageConverterProvider,
                deviceClientProvider.Object,
                Mock.Of<ICloudListener>(),
                TimeSpan.FromMinutes(60),
                true,
                TimeSpan.FromSeconds(20),
                DummyProductInfo,
                Option.None<string>());
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
                tokenCredentials,
                (_, __) => { },
                transportSettings,
                messageConverterProvider,
                deviceClientProvider.Object,
                Mock.Of<ICloudListener>(),
                TimeSpan.FromMinutes(60),
                true,
                TimeSpan.FromSeconds(20),
                DummyProductInfo,
                Option.None<string>()));
        }

        [Fact]
        [Unit]
        public async Task UpdateTokenTest()
        {
            var initialTokenCredentials = GetMockClientCredentialsWithToken();
            var identity = initialTokenCredentials.Identity;
            var token = initialTokenCredentials.Token;

            ITokenProvider tokenProvider = null;
            var client = new Mock<IClient>();
            var deviceClientProvider = new Mock<IClientProvider>();
            deviceClientProvider.Setup(dc => dc.Create(It.IsAny<IIdentity>(), It.IsAny<ITokenProvider>(), It.IsAny<ITransportSettings[]>(), Option.None<string>()))
                .Callback<IIdentity, ITokenProvider, ITransportSettings[], Option<string>>((id, tp, ts, mid) => tokenProvider = tp)
                .Returns(() => client.Object);
            client.Setup(c => c.IsActive)
                .Returns(true);
            client.Setup(c => c.OpenAsync())
                .Returns(async () => await tokenProvider.GetTokenAsync(Option.None<TimeSpan>()));
            var transportSettings = new ITransportSettings[] { new AmqpTransportSettings(TransportType.Amqp_Tcp_Only) };
            var messageConverterProvider = new MessageConverterProvider(new Dictionary<Type, IMessageConverter> { [typeof(TwinCollection)] = Mock.Of<IMessageConverter>() });

            var cloudConnection = await Create(
                initialTokenCredentials,
                (_, __) => { },
                transportSettings,
                messageConverterProvider,
                deviceClientProvider.Object,
                Mock.Of<ICloudListener>(),
                TimeSpan.FromMinutes(60),
                true,
                TimeSpan.FromSeconds(20),
                DummyProductInfo,
                Option.None<string>());

            Assert.NotNull(tokenProvider);
            Assert.Equal(initialTokenCredentials.Token, await tokenProvider.GetTokenAsync(Option.None<TimeSpan>()));
            Option<ICloudProxy> cloudProxy = cloudConnection.CloudProxy;
            Assert.True(cloudProxy.HasValue);
            Assert.True(cloudProxy.OrDefault().IsActive);

            var updatedTokenCredentials = GetMockClientCredentialsWithToken();
            await cloudConnection.UpdateTokenAsync(updatedTokenCredentials);
            Assert.Equal(updatedTokenCredentials.Token, await tokenProvider.GetTokenAsync(Option.None<TimeSpan>()));
            Assert.True(cloudProxy.HasValue);
            Assert.True(cloudProxy.OrDefault().IsActive);
        }

        [Fact]
        [Unit]
        public async Task UpdateTokenFailureTest()
        {
            var initialTokenCredentials = GetMockClientCredentialsWithToken();
            var identity = initialTokenCredentials.Identity;

            ITokenProvider tokenProvider = null;
            var deviceClientProvider = new Mock<IClientProvider>();
            var client = new Mock<IClient>();
            client.Setup(c => c.IsActive)
                .Returns(true);
            deviceClientProvider.Setup(dc => dc.Create(It.IsAny<IIdentity>(), It.IsAny<ITokenProvider>(), It.IsAny<ITransportSettings[]>(), Option.None<string>()))
                .Callback<IIdentity, ITokenProvider, ITransportSettings[], Option<string>>((id, tp, ts, mid) => tokenProvider = tp)
                .Returns(() => client.Object);
            client.Setup(c => c.OpenAsync())
                .Returns(async () => await tokenProvider.GetTokenAsync(Option.None<TimeSpan>()));
            var transportSettings = new ITransportSettings[] { new AmqpTransportSettings(TransportType.Amqp_Tcp_Only) };
            var messageConverterProvider = new MessageConverterProvider(new Dictionary<Type, IMessageConverter> { [typeof(TwinCollection)] = Mock.Of<IMessageConverter>() });

            var cloudConnection = await Create(
                initialTokenCredentials,
                (_, __) => { },
                transportSettings,
                messageConverterProvider,
                deviceClientProvider.Object,
                Mock.Of<ICloudListener>(),
                TimeSpan.FromMinutes(60),
                true,
                TimeSpan.FromSeconds(20),
                DummyProductInfo,
                Option.None<string>());

            Assert.NotNull(tokenProvider);
            Assert.Equal(initialTokenCredentials.Token, await tokenProvider.GetTokenAsync(Option.None<TimeSpan>()));
            Option<ICloudProxy> cloudProxy = cloudConnection.CloudProxy;
            Assert.True(cloudProxy.HasValue);
            Assert.True(cloudProxy.OrDefault().IsActive);

            var expiredTokenCredentials = GetMockClientCredentialsWithToken(expired:true);
            await Assert.ThrowsAsync<ArgumentException>(() => cloudConnection.UpdateTokenAsync(expiredTokenCredentials));
            Assert.Equal(initialTokenCredentials.Token, await tokenProvider.GetTokenAsync(Option.None<TimeSpan>()));
            Assert.True(cloudProxy.HasValue);
            Assert.True(cloudProxy.OrDefault().IsActive);

            var invalidDeviceCredentials = GetMockClientCredentialsWithToken(deviceId: "testDevice2");
            await Assert.ThrowsAsync<ArgumentException>(() => cloudConnection.UpdateTokenAsync(expiredTokenCredentials));
            Assert.Equal(initialTokenCredentials.Token, await tokenProvider.GetTokenAsync(Option.None<TimeSpan>()));
            Assert.True(cloudProxy.HasValue);
            Assert.True(cloudProxy.OrDefault().IsActive);

            var invalidHostCredentials = GetMockClientCredentialsWithToken(hostname: "dummy2.azure-devices.net");
            await Assert.ThrowsAsync<ArgumentException>(() => cloudConnection.UpdateTokenAsync(invalidHostCredentials));
            Assert.Equal(initialTokenCredentials.Token, await tokenProvider.GetTokenAsync(Option.None<TimeSpan>()));
            Assert.True(cloudProxy.HasValue);
            Assert.True(cloudProxy.OrDefault().IsActive);
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
