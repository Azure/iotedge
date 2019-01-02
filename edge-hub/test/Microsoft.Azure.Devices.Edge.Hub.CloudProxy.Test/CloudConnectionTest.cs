// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy.Test
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Shared;
    using Moq;
    using Xunit;
    [Unit]
    public class CloudConnectionTest
    {        
        [Fact]
        public async Task GetCloudConnectionForIdentityWithKeyTest()
        {
            IClientProvider clientProvider = GetMockDeviceClientProviderWithKey();
            var tokenProvider = Mock.Of<ITokenProvider>();
            var identity = Mock.Of<IIdentity>(i => i.Id == "d1");
            var transportSettings = new ITransportSettings[] { new AmqpTransportSettings(TransportType.Amqp_Tcp_Only) };
            var messageConverterProvider = new MessageConverterProvider(new Dictionary<Type, IMessageConverter> { [typeof(TwinCollection)] = Mock.Of<IMessageConverter>() });
            CloudConnection cloudConnection = await CloudConnection.Create(
                identity,
                (_, __) => { },
                transportSettings,
                messageConverterProvider,
                clientProvider,
                Mock.Of<ICloudListener>(),
                tokenProvider,
                TimeSpan.FromMinutes(60),
                true,
                TimeSpan.FromSeconds(20));

            Option<ICloudProxy> cloudProxy1 = cloudConnection.CloudProxy;
            Assert.True(cloudProxy1.HasValue);
            Assert.True(cloudProxy1.OrDefault().IsActive);
        }

        [Fact]
        public async Task GetCloudConnectionThrowsTest()
        {
            var deviceClient = new Mock<IClient>();
            deviceClient.SetupGet(dc => dc.IsActive).Returns(true);
            deviceClient.Setup(dc => dc.CloseAsync())
                .Callback(() => deviceClient.SetupGet(dc => dc.IsActive).Returns(false))
                .Returns(Task.FromResult(true));
            deviceClient.Setup(dc => dc.OpenAsync()).ThrowsAsync(new TimeoutException());

            var deviceClientProvider = new Mock<IClientProvider>();
            deviceClientProvider.Setup(dc => dc.Create(It.IsAny<IIdentity>(), It.IsAny<ITokenProvider>(), It.IsAny<ITransportSettings[]>()))
                .Returns(deviceClient.Object);

            var tokenProvider = Mock.Of<ITokenProvider>();
            var identity = Mock.Of<IIdentity>(i => i.Id == "d1");
            var transportSettings = new ITransportSettings[] { new AmqpTransportSettings(TransportType.Amqp_Tcp_Only) };
            var messageConverterProvider = new MessageConverterProvider(new Dictionary<Type, IMessageConverter> { [typeof(TwinCollection)] = Mock.Of<IMessageConverter>() });
            await Assert.ThrowsAsync<TimeoutException>(() => CloudConnection.Create(
                identity,
                (_, __) => { },
                transportSettings,
                messageConverterProvider,
                deviceClientProvider.Object,
                Mock.Of<ICloudListener>(),
                tokenProvider,
                TimeSpan.FromMinutes(60),
                true,
                TimeSpan.FromSeconds(20)));
        }

        static IClientProvider GetMockDeviceClientProviderWithKey()
        {
            var deviceClientProvider = new Mock<IClientProvider>();
            deviceClientProvider.Setup(dc => dc.Create(It.IsAny<IIdentity>(), It.IsAny<ITokenProvider>(), It.IsAny<ITransportSettings[]>()))
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
    }
}
