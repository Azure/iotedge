// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy.Test
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity.Service;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Unit]
    public class CloudConnectionProviderTest
    {
        static readonly IMessageConverterProvider MessageConverterProvider = Mock.Of<IMessageConverterProvider>();
        static readonly ITokenProvider TokenProvider = Mock.Of<ITokenProvider>();
        static readonly IDeviceScopeIdentitiesCache DeviceScopeIdentitiesCache = Mock.Of<IDeviceScopeIdentitiesCache>();
        static readonly ICredentialsCache CredentialsCache = Mock.Of<ICredentialsCache>();
        static readonly IIdentity EdgeHubIdentity = Mock.Of<IIdentity>(i => i.Id == "device1/$edgeHub");
        const string IotHubHostName = "foo.azure-devices.net";

        const int ConnectionPoolSize = 10;

        [Fact]
        public async Task ConnectUsingTokenCredentialsTest()
        {
            // Arrange
            var edgeHub = Mock.Of<IEdgeHub>();
            ICloudConnectionProvider cloudConnectionProvider = new CloudConnectionProvider(
                MessageConverterProvider,
                ConnectionPoolSize,
                GetMockDeviceClientProvider(),
                Option.None<UpstreamProtocol>(),
                TokenProvider,
                DeviceScopeIdentitiesCache,
                CredentialsCache,
                EdgeHubIdentity,
                TimeSpan.FromMinutes(60),
                true,
                TimeSpan.FromSeconds(20));
            cloudConnectionProvider.BindEdgeHub(edgeHub);
            var deviceIdentity = Mock.Of<IDeviceIdentity>(m => m.Id == "d1");
            string token = TokenHelper.CreateSasToken(IotHubHostName, DateTime.UtcNow.AddMinutes(10));
            var tokenCreds = new TokenCredentials(deviceIdentity, token, string.Empty, false);

            // Act
            Try<ICloudConnection> cloudProxy = await cloudConnectionProvider.Connect(tokenCreds, null);

            // Assert
            Assert.True(cloudProxy.Success);
            Assert.NotNull(cloudProxy.Value);
        }

        [Fact]
        public async Task ConnectUsingInvalidTokenCredentialsTest()
        {
            // Arrange
            var deviceClient = new Mock<IClient>();
            deviceClient.SetupGet(dc => dc.IsActive).Returns(true);
            deviceClient.Setup(dc => dc.CloseAsync())
                .Callback(() => deviceClient.SetupGet(dc => dc.IsActive).Returns(false))
                .Returns(Task.FromResult(true));
            deviceClient.Setup(dc => dc.OpenAsync()).ThrowsAsync(new TimeoutException());

            var deviceClientProvider = new Mock<IClientProvider>();
            deviceClientProvider.Setup(dc => dc.Create(It.IsAny<IIdentity>(), It.IsAny<ITokenProvider>(), It.IsAny<ITransportSettings[]>()))
                .Returns(deviceClient.Object);

            var edgeHub = Mock.Of<IEdgeHub>();
            ICloudConnectionProvider cloudConnectionProvider = new CloudConnectionProvider(
                MessageConverterProvider,
                ConnectionPoolSize,
                deviceClientProvider.Object,
                Option.None<UpstreamProtocol>(),
                TokenProvider,
                DeviceScopeIdentitiesCache,
                CredentialsCache,
                EdgeHubIdentity,
                TimeSpan.FromMinutes(60),
                true,
                TimeSpan.FromSeconds(20));
            cloudConnectionProvider.BindEdgeHub(edgeHub);
            var deviceIdentity = Mock.Of<IDeviceIdentity>(m => m.Id == "d1");
            string token = TokenHelper.CreateSasToken(IotHubHostName, DateTime.UtcNow.AddMinutes(10));
            var tokenCreds = new TokenCredentials(deviceIdentity, token, string.Empty, false);

            // Act
            Try<ICloudConnection> cloudProxy = await cloudConnectionProvider.Connect(tokenCreds, null);

            // Assert
            Assert.False(cloudProxy.Success);
            Assert.IsType<TimeoutException>(cloudProxy.Exception);
        }

        [Fact]
        public async Task ConnectUsingIdentityInScopeTest()
        {
            // Arrange            
            var deviceIdentity = Mock.Of<IDeviceIdentity>(m => m.Id == "d1");

            var deviceScopeIdentitiesCache = new Mock<IDeviceScopeIdentitiesCache>(MockBehavior.Strict);
            var deviceServiceIdentity = new ServiceIdentity(deviceIdentity.Id, "1234", new string[0], new ServiceAuthentication(ServiceAuthenticationType.CertificateAuthority), ServiceIdentityStatus.Enabled);
            deviceScopeIdentitiesCache.Setup(d => d.GetServiceIdentity(It.Is<string>(i => i == deviceIdentity.Id), false))
                .ReturnsAsync(Option.Some(deviceServiceIdentity));

            var edgeHub = Mock.Of<IEdgeHub>();
            ICloudConnectionProvider cloudConnectionProvider = new CloudConnectionProvider(
                MessageConverterProvider,
                ConnectionPoolSize,
                GetMockDeviceClientProvider(),
                Option.None<UpstreamProtocol>(),
                TokenProvider,
                deviceScopeIdentitiesCache.Object,
                CredentialsCache,
                EdgeHubIdentity,
                TimeSpan.FromMinutes(60),
                true,
                TimeSpan.FromSeconds(20));
            cloudConnectionProvider.BindEdgeHub(edgeHub);

            // Act
            Try<ICloudConnection> cloudProxy = await cloudConnectionProvider.Connect(deviceIdentity, null);

            // Assert
            Assert.True(cloudProxy.Success);
            Assert.NotNull(cloudProxy.Value);
            deviceScopeIdentitiesCache.VerifyAll();
        }

        [Fact]
        public async Task ConnectUsingIdentityInCacheTest()
        {
            // Arrange            
            var deviceIdentity = Mock.Of<IDeviceIdentity>(m => m.Id == "d1");
            string token = TokenHelper.CreateSasToken(IotHubHostName, DateTime.UtcNow.AddMinutes(10));
            var tokenCreds = new TokenCredentials(deviceIdentity, token, string.Empty, false);

            var deviceScopeIdentitiesCache = new Mock<IDeviceScopeIdentitiesCache>(MockBehavior.Strict);
            var deviceServiceIdentity = new ServiceIdentity(deviceIdentity.Id, "1234", new string[0], new ServiceAuthentication(ServiceAuthenticationType.CertificateAuthority), ServiceIdentityStatus.Disabled);
            deviceScopeIdentitiesCache.Setup(d => d.GetServiceIdentity(It.Is<string>(i => i == deviceIdentity.Id), false))
                .ReturnsAsync(Option.Some(deviceServiceIdentity));

            var credentialsCache = new Mock<ICredentialsCache>(MockBehavior.Strict);
            credentialsCache.Setup(c => c.Get(deviceIdentity)).ReturnsAsync(Option.Some((IClientCredentials)tokenCreds));

            var edgeHub = Mock.Of<IEdgeHub>();
            ICloudConnectionProvider cloudConnectionProvider = new CloudConnectionProvider(
                MessageConverterProvider,
                ConnectionPoolSize,
                GetMockDeviceClientProvider(),
                Option.None<UpstreamProtocol>(),
                TokenProvider,
                deviceScopeIdentitiesCache.Object,
                credentialsCache.Object,
                EdgeHubIdentity,
                TimeSpan.FromMinutes(60),
                true,
                TimeSpan.FromSeconds(20));
            cloudConnectionProvider.BindEdgeHub(edgeHub);

            // Act
            Try<ICloudConnection> cloudProxy = await cloudConnectionProvider.Connect(deviceIdentity, null);

            // Assert
            Assert.True(cloudProxy.Success);
            Assert.NotNull(cloudProxy.Value);
            deviceScopeIdentitiesCache.VerifyAll();
            credentialsCache.VerifyAll();
        }

        [Fact]
        public async Task ConnectUsingIdentityInCacheTest2()
        {
            // Arrange            
            var deviceIdentity = Mock.Of<IDeviceIdentity>(m => m.Id == "d1");
            string token = TokenHelper.CreateSasToken(IotHubHostName, DateTime.UtcNow.AddMinutes(10));
            var tokenCreds = new TokenCredentials(deviceIdentity, token, string.Empty, false);

            var deviceScopeIdentitiesCache = new Mock<IDeviceScopeIdentitiesCache>(MockBehavior.Strict);
            deviceScopeIdentitiesCache.Setup(d => d.GetServiceIdentity(It.Is<string>(i => i == deviceIdentity.Id), false))
                .ReturnsAsync(Option.None<ServiceIdentity>());

            var credentialsCache = new Mock<ICredentialsCache>(MockBehavior.Strict);
            credentialsCache.Setup(c => c.Get(deviceIdentity)).ReturnsAsync(Option.Some((IClientCredentials)tokenCreds));

            var edgeHub = Mock.Of<IEdgeHub>();
            ICloudConnectionProvider cloudConnectionProvider = new CloudConnectionProvider(
                MessageConverterProvider,
                ConnectionPoolSize,
                GetMockDeviceClientProvider(),
                Option.None<UpstreamProtocol>(),
                TokenProvider,
                deviceScopeIdentitiesCache.Object,
                credentialsCache.Object,
                EdgeHubIdentity,
                TimeSpan.FromMinutes(60),
                true,
                TimeSpan.FromSeconds(20));
            cloudConnectionProvider.BindEdgeHub(edgeHub);

            // Act
            Try<ICloudConnection> cloudProxy = await cloudConnectionProvider.Connect(deviceIdentity, null);

            // Assert
            Assert.True(cloudProxy.Success);
            Assert.NotNull(cloudProxy.Value);
            deviceScopeIdentitiesCache.VerifyAll();
            credentialsCache.VerifyAll();
        }

        [Theory]
        [MemberData(nameof(UpstreamProtocolTransportSettingsData))]
        public void GetTransportSettingsTest(Option<UpstreamProtocol> upstreamProtocol, int connectionPoolSize, ITransportSettings[] expectedTransportSettingsList)
        {
            ITransportSettings[] transportSettingsList = CloudConnectionProvider.GetTransportSettings(upstreamProtocol, connectionPoolSize);

            Assert.NotNull(transportSettingsList);
            Assert.Equal(expectedTransportSettingsList.Length, transportSettingsList.Length);
            for (int i = 0; i < expectedTransportSettingsList.Length; i++)
            {
                ITransportSettings expectedTransportSettings = expectedTransportSettingsList[i];
                ITransportSettings transportSettings = transportSettingsList[i];

                Assert.Equal(expectedTransportSettings.GetType(), transportSettings.GetType());
                Assert.Equal(expectedTransportSettings.GetTransportType(), transportSettings.GetTransportType());
                if (expectedTransportSettings is AmqpTransportSettings)
                {
                    Assert.True(((AmqpTransportSettings)expectedTransportSettings).Equals((AmqpTransportSettings)transportSettings));
                }
            }
        }

        public static IEnumerable<object[]> UpstreamProtocolTransportSettingsData()
        {
            yield return new object[]
            {
                Option.None<UpstreamProtocol>(),
                20,
                new ITransportSettings[]
                {
                    new AmqpTransportSettings(TransportType.Amqp_Tcp_Only)
                    {
                        AmqpConnectionPoolSettings = new AmqpConnectionPoolSettings
                        {
                            Pooling = true,
                            MaxPoolSize = 20,
                            ConnectionIdleTimeout = TimeSpan.FromSeconds(5)
                        }
                    }
                }
            };

            yield return new object[]
            {
                Option.Some(UpstreamProtocol.Amqp),
                30,
                new ITransportSettings[]
                {
                    new AmqpTransportSettings(TransportType.Amqp_Tcp_Only)
                    {
                        AmqpConnectionPoolSettings = new AmqpConnectionPoolSettings
                        {
                            Pooling = true,
                            MaxPoolSize = 30,
                            ConnectionIdleTimeout = TimeSpan.FromSeconds(5)
                        }
                    }
                }
            };

            yield return new object[]
            {
                Option.Some(UpstreamProtocol.AmqpWs),
                50,
                new ITransportSettings[]
                {
                    new AmqpTransportSettings(TransportType.Amqp_WebSocket_Only)
                    {
                        AmqpConnectionPoolSettings = new AmqpConnectionPoolSettings
                        {
                            Pooling = true,
                            MaxPoolSize = 50,
                            ConnectionIdleTimeout = TimeSpan.FromSeconds(5)
                        }
                    }
                }
            };

            yield return new object[]
            {
                Option.Some(UpstreamProtocol.Mqtt),
                60,
                new ITransportSettings[]
                {
                    new MqttTransportSettings(TransportType.Mqtt_Tcp_Only)
                }
            };

            yield return new object[]
            {
                Option.Some(UpstreamProtocol.MqttWs),
                80,
                new ITransportSettings[]
                {
                    new MqttTransportSettings(TransportType.Mqtt_WebSocket_Only)
                }
            };
        }

        static IClientProvider GetMockDeviceClientProvider()
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
