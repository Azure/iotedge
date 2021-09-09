// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy.Test
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Reflection;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Exceptions;
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
    using TransportType = Microsoft.Azure.Devices.Client.TransportType;

    [Unit]
    public class CloudConnectionProviderTest
    {
        const string IotHubHostName = "foo.azure-devices.net";
        const string ProxyUri = "http://proxyserver:1234";
        const int ConnectionPoolSize = 10;
        static readonly IMessageConverterProvider MessageConverterProvider = Mock.Of<IMessageConverterProvider>();
        static readonly ITokenProvider TokenProvider = Mock.Of<ITokenProvider>();
        static readonly ICredentialsCache CredentialsCache = Mock.Of<ICredentialsCache>();
        static readonly IIdentity EdgeHubIdentity = Mock.Of<IIdentity>(i => i.Id == "device1/$edgeHub");

        public static IEnumerable<object[]> UpstreamProtocolTransportSettingsData()
        {
            yield return new object[]
            {
                Option.None<UpstreamProtocol>(),
                20,
                Option.None<IWebProxy>(),
                new ITransportSettings[]
                {
                    new AmqpTransportSettings(TransportType.Amqp_Tcp_Only)
                    {
                        AmqpConnectionPoolSettings = new AmqpConnectionPoolSettings
                        {
                            Pooling = true,
                            MaxPoolSize = 20
                        },
                        IdleTimeout = TimeSpan.FromSeconds(0)
                    }
                },
                false
            };

            yield return new object[]
            {
                Option.Some(UpstreamProtocol.Amqp),
                30,
                Option.Some(new WebProxy(ProxyUri) as IWebProxy),
                new ITransportSettings[]
                {
                    new AmqpTransportSettings(TransportType.Amqp_Tcp_Only)
                    {
                        AmqpConnectionPoolSettings = new AmqpConnectionPoolSettings
                        {
                            Pooling = true,
                            MaxPoolSize = 30
                        },
                        Proxy = new WebProxy(ProxyUri),
                        IdleTimeout = TimeSpan.FromSeconds(60)
                    }
                },
                true
            };

            yield return new object[]
            {
                Option.Some(UpstreamProtocol.AmqpWs),
                50,
                Option.Some(new WebProxy(ProxyUri) as IWebProxy),
                new ITransportSettings[]
                {
                    new AmqpTransportSettings(TransportType.Amqp_WebSocket_Only)
                    {
                        AmqpConnectionPoolSettings = new AmqpConnectionPoolSettings
                        {
                            Pooling = true,
                            MaxPoolSize = 50
                        },
                        Proxy = new WebProxy(ProxyUri)
                    }
                },
                false
            };

            yield return new object[]
            {
                Option.Some(UpstreamProtocol.Mqtt),
                60,
                Option.Some(new WebProxy(ProxyUri) as IWebProxy),
                new ITransportSettings[]
                {
                    new MqttTransportSettings(TransportType.Mqtt_Tcp_Only)
                    {
                        Proxy = new WebProxy(ProxyUri)
                    }
                },
                false
            };

            yield return new object[]
            {
                Option.Some(UpstreamProtocol.MqttWs),
                80,
                Option.Some(new WebProxy(ProxyUri) as IWebProxy),
                new ITransportSettings[]
                {
                    new MqttTransportSettings(TransportType.Mqtt_WebSocket_Only)
                    {
                        Proxy = new WebProxy(ProxyUri)
                    }
                },
                true
            };
        }

        [Fact]
        public async Task ConnectUsingTokenCredentialsTest()
        {
            // Arrange
            var metadataStore = new Mock<IMetadataStore>();
            metadataStore.Setup(m => m.GetMetadata(It.IsAny<string>())).ReturnsAsync(new ConnectionMetadata("dummyValue"));
            var edgeHub = Mock.Of<IEdgeHub>();
            var deviceScopeIdentitiesCache = new Mock<IDeviceScopeIdentitiesCache>(MockBehavior.Strict);

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
                TimeSpan.FromSeconds(20),
                false,
                Option.None<IWebProxy>(),
                metadataStore.Object,
                true,
                true,
                true);
            cloudConnectionProvider.BindEdgeHub(edgeHub);

            var deviceIdentity = Mock.Of<IDeviceIdentity>(m => m.Id == "d1");
            deviceScopeIdentitiesCache.Setup(d => d.GetAuthChain(It.Is<string>(i => i == deviceIdentity.Id)))
                .ReturnsAsync(Option.Some(deviceIdentity.Id));
            string token = TokenHelper.CreateSasToken(IotHubHostName, DateTime.UtcNow.AddMinutes(10));
            var tokenCreds = new TokenCredentials(deviceIdentity, token, string.Empty, Option.None<string>(), Option.None<string>(), false);

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
            deviceClientProvider.Setup(dc => dc.Create(It.IsAny<IIdentity>(), It.IsAny<ITokenProvider>(), It.IsAny<ITransportSettings[]>(), Option.None<string>()))
                .Returns(deviceClient.Object);
            var metadataStore = new Mock<IMetadataStore>();
            metadataStore.Setup(m => m.GetMetadata(It.IsAny<string>())).ReturnsAsync(new ConnectionMetadata("dummyValue"));
            var edgeHub = Mock.Of<IEdgeHub>();
            var deviceScopeIdentitiesCache = new Mock<IDeviceScopeIdentitiesCache>(MockBehavior.Strict);

            ICloudConnectionProvider cloudConnectionProvider = new CloudConnectionProvider(
                MessageConverterProvider,
                ConnectionPoolSize,
                deviceClientProvider.Object,
                Option.None<UpstreamProtocol>(),
                TokenProvider,
                deviceScopeIdentitiesCache.Object,
                CredentialsCache,
                EdgeHubIdentity,
                TimeSpan.FromMinutes(60),
                true,
                TimeSpan.FromSeconds(20),
                false,
                Option.None<IWebProxy>(),
                metadataStore.Object,
                true,
                true,
                true);
            cloudConnectionProvider.BindEdgeHub(edgeHub);

            var deviceIdentity = Mock.Of<IDeviceIdentity>(m => m.Id == "d1");
            deviceScopeIdentitiesCache.Setup(d => d.GetAuthChain(It.Is<string>(i => i == deviceIdentity.Id)))
                .ReturnsAsync(Option.Some(deviceIdentity.Id));
            string token = TokenHelper.CreateSasToken(IotHubHostName, DateTime.UtcNow.AddMinutes(10));
            var tokenCreds = new TokenCredentials(deviceIdentity, token, string.Empty, Option.None<string>(), Option.None<string>(), false);

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
            deviceScopeIdentitiesCache.Setup(d => d.GetServiceIdentity(It.Is<string>(i => i == deviceIdentity.Id)))
                .ReturnsAsync(Option.Some(deviceServiceIdentity));
            deviceScopeIdentitiesCache.Setup(d => d.GetAuthChain(It.Is<string>(i => i == deviceIdentity.Id)))
                .ReturnsAsync(Option.Some(deviceServiceIdentity.Id));
            var metadataStore = new Mock<IMetadataStore>();
            metadataStore.Setup(m => m.GetMetadata(It.IsAny<string>())).ReturnsAsync(new ConnectionMetadata("dummyValue"));

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
                TimeSpan.FromSeconds(20),
                false,
                Option.None<IWebProxy>(),
                metadataStore.Object,
                scopeAuthenticationOnly: true,
                trackDeviceState: false);
            cloudConnectionProvider.BindEdgeHub(edgeHub);

            // Act
            Try<ICloudConnection> cloudProxy = await cloudConnectionProvider.Connect(deviceIdentity, null);

            // Assert
            Assert.True(cloudProxy.Success);
            Assert.NotNull(cloudProxy.Value);
            deviceScopeIdentitiesCache.VerifyAll();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ConnectUsingIdentityInScope_DeviceInvalidStateException_ShouldThrow(bool isNested)
        {
            // Arrange
            var deviceIdentity = Mock.Of<IDeviceIdentity>(m => m.Id == "d1");

            var deviceScopeIdentitiesCache = new Mock<IDeviceScopeIdentitiesCache>(MockBehavior.Strict);
            var deviceServiceIdentity = new ServiceIdentity(deviceIdentity.Id, "1234", new string[0], new ServiceAuthentication(ServiceAuthenticationType.CertificateAuthority), ServiceIdentityStatus.Disabled);

            deviceScopeIdentitiesCache.Setup(d => d.VerifyServiceIdentityAuthChainState(It.Is<string>(i => i == deviceIdentity.Id), isNested, false))
            .ThrowsAsync(new DeviceInvalidStateException());
            deviceScopeIdentitiesCache.Setup(d => d.VerifyServiceIdentityAuthChainState(It.Is<string>(i => i == deviceIdentity.Id), isNested, true))
                .ThrowsAsync(new DeviceInvalidStateException());

            var metadataStore = new Mock<IMetadataStore>();
            metadataStore.Setup(m => m.GetMetadata(It.IsAny<string>())).ReturnsAsync(new ConnectionMetadata("dummyValue"));
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
                TimeSpan.FromSeconds(20),
                false,
                Option.None<IWebProxy>(),
                metadataStore.Object,
                scopeAuthenticationOnly: true,
                trackDeviceState: true,
                nestedEdgeEnabled: isNested);
            cloudConnectionProvider.BindEdgeHub(edgeHub);

            // Act
            Try<ICloudConnection> cloudProxy = await cloudConnectionProvider.Connect(deviceIdentity, null);

            // Assert
            Assert.Throws<DeviceInvalidStateException>(() => cloudProxy.Value);
            deviceScopeIdentitiesCache.VerifyAll();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ConnectUsingIdentityInScope_DeviceInvalidStateException_WithRecover(bool isNested)
        {
            // Arrange
            var deviceIdentity = Mock.Of<IDeviceIdentity>(m => m.Id == "d1");

            var deviceScopeIdentitiesCache = new Mock<IDeviceScopeIdentitiesCache>(MockBehavior.Strict);
            var deviceServiceIdentity = new ServiceIdentity(deviceIdentity.Id, "1234", new string[0], new ServiceAuthentication(ServiceAuthenticationType.CertificateAuthority), ServiceIdentityStatus.Disabled);

            deviceScopeIdentitiesCache.Setup(d => d.VerifyServiceIdentityAuthChainState(It.Is<string>(i => i == deviceIdentity.Id), isNested, false))
            .ThrowsAsync(new DeviceInvalidStateException());
            deviceScopeIdentitiesCache.Setup(d => d.VerifyServiceIdentityAuthChainState(It.Is<string>(i => i == deviceIdentity.Id), isNested, true))
                .ReturnsAsync(deviceIdentity.Id);

            var metadataStore = new Mock<IMetadataStore>();
            metadataStore.Setup(m => m.GetMetadata(It.IsAny<string>())).ReturnsAsync(new ConnectionMetadata("dummyValue"));
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
                TimeSpan.FromSeconds(20),
                false,
                Option.None<IWebProxy>(),
                metadataStore.Object,
                scopeAuthenticationOnly: true,
                trackDeviceState: true,
                nestedEdgeEnabled: isNested);
            cloudConnectionProvider.BindEdgeHub(edgeHub);

            // Act
            Try<ICloudConnection> cloudProxy = await cloudConnectionProvider.Connect(deviceIdentity, null);

            // Assert
            Assert.True(cloudProxy.Success);
            Assert.NotNull(cloudProxy.Value);
            deviceScopeIdentitiesCache.VerifyAll();
        }

        [Fact]
        public async Task ConnectUsingIdentityInScope_NestedEdge_DeviceInvalidStateException_WithRecover()
        {
            // Arrange
            var deviceIdentity = Mock.Of<IDeviceIdentity>(m => m.Id == "d1");

            var deviceScopeIdentitiesCache = new Mock<IDeviceScopeIdentitiesCache>(MockBehavior.Strict);
            var deviceServiceIdentity = new ServiceIdentity(deviceIdentity.Id, "1234", new string[0], new ServiceAuthentication(ServiceAuthenticationType.CertificateAuthority), ServiceIdentityStatus.Disabled);
            deviceScopeIdentitiesCache.Setup(d => d.VerifyServiceIdentityAuthChainState(It.Is<string>(i => i == deviceIdentity.Id), true, false))
                .ThrowsAsync(new DeviceInvalidStateException());
            deviceScopeIdentitiesCache.Setup(d => d.VerifyServiceIdentityAuthChainState(It.Is<string>(i => i == deviceIdentity.Id), true, true))
                .ReturnsAsync(deviceIdentity.Id);
            var metadataStore = new Mock<IMetadataStore>();
            metadataStore.Setup(m => m.GetMetadata(It.IsAny<string>())).ReturnsAsync(new ConnectionMetadata("dummyValue"));
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
                TimeSpan.FromSeconds(20),
                false,
                Option.None<IWebProxy>(),
                metadataStore.Object,
                scopeAuthenticationOnly: true,
                trackDeviceState: true,
                nestedEdgeEnabled: true);
            cloudConnectionProvider.BindEdgeHub(edgeHub);

            // Act
            Try<ICloudConnection> cloudProxy = await cloudConnectionProvider.Connect(deviceIdentity, null);

            // Assert
            Assert.True(cloudProxy.Success);
            Assert.NotNull(cloudProxy.Value);
            deviceScopeIdentitiesCache.VerifyAll();
        }

        [Fact]
        public async Task Connect_ScopeOnly_TokenCredentials()
        {
            var deviceIdentity = Mock.Of<IDeviceIdentity>(m => m.Id == "d1");

            var deviceScopeIdentitiesCache = new Mock<IDeviceScopeIdentitiesCache>(MockBehavior.Strict);
            deviceScopeIdentitiesCache.Setup(d => d.GetServiceIdentity(It.Is<string>(i => i == deviceIdentity.Id)))
                .ReturnsAsync(Option.None<ServiceIdentity>());
            deviceScopeIdentitiesCache.Setup(d => d.GetAuthChain(It.Is<string>(i => i == deviceIdentity.Id)))
                .ReturnsAsync(Option.Some(deviceIdentity.Id));
            var metadataStore = new Mock<IMetadataStore>();
            metadataStore.Setup(m => m.GetMetadata(It.IsAny<string>())).ReturnsAsync(new ConnectionMetadata("dummyValue"));
            var edgeHub = Mock.Of<IEdgeHub>();
            var credentialsCache = new Mock<ICredentialsCache>(MockBehavior.Strict);
            string token = TokenHelper.CreateSasToken(IotHubHostName, DateTime.UtcNow.AddMinutes(10));
            var tokenCreds = new TokenCredentials(deviceIdentity, token, string.Empty, Option.None<string>(), Option.None<string>(), false);
            credentialsCache.Setup(cc => cc.Get(deviceIdentity)).ReturnsAsync(Option.Some<IClientCredentials>(tokenCreds));
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
                TimeSpan.FromSeconds(20),
                false,
                Option.None<IWebProxy>(),
                metadataStore.Object,
                scopeAuthenticationOnly: true,
                trackDeviceState: false);
            cloudConnectionProvider.BindEdgeHub(edgeHub);

            // Act
            Try<ICloudConnection> cloudProxy = await cloudConnectionProvider.Connect(deviceIdentity, null);

            // Assert
            Assert.Throws<InvalidOperationException>(() => cloudProxy.Value);
            deviceScopeIdentitiesCache.VerifyAll();
            credentialsCache.Verify(cc => cc.Get(It.IsAny<IIdentity>()), Times.Never);
        }

        [Fact]
        public async Task Connect_Fallback_TokenCredentials()
        {
            var deviceIdentity = Mock.Of<IDeviceIdentity>(m => m.Id == "d1");

            var deviceScopeIdentitiesCache = new Mock<IDeviceScopeIdentitiesCache>(MockBehavior.Strict);
            deviceScopeIdentitiesCache.Setup(d => d.GetServiceIdentity(It.Is<string>(i => i == deviceIdentity.Id)))
                .ReturnsAsync(Option.None<ServiceIdentity>());
            deviceScopeIdentitiesCache.Setup(d => d.GetAuthChain(It.Is<string>(i => i == deviceIdentity.Id)))
               .ReturnsAsync(Option.Some(deviceIdentity.Id));
            var metadataStore = new Mock<IMetadataStore>();
            metadataStore.Setup(m => m.GetMetadata(It.IsAny<string>())).ReturnsAsync(new ConnectionMetadata("dummyValue"));
            var edgeHub = Mock.Of<IEdgeHub>();
            var credentialsCache = new Mock<ICredentialsCache>(MockBehavior.Strict);
            string token = TokenHelper.CreateSasToken(IotHubHostName, DateTime.UtcNow.AddMinutes(10));
            var tokenCreds = new TokenCredentials(deviceIdentity, token, string.Empty, Option.None<string>(), Option.None<string>(), false);
            credentialsCache.Setup(cc => cc.Get(deviceIdentity)).ReturnsAsync(Option.Some<IClientCredentials>(tokenCreds));
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
                TimeSpan.FromSeconds(20),
                false,
                Option.None<IWebProxy>(),
                metadataStore.Object,
                scopeAuthenticationOnly: false,
                trackDeviceState: false,
                true);
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
        public async Task ConnectUsingIdentityInCacheTest()
        {
            // Arrange
            var deviceIdentity = Mock.Of<IDeviceIdentity>(m => m.Id == "d1");
            string token = TokenHelper.CreateSasToken(IotHubHostName, DateTime.UtcNow.AddMinutes(10));
            var tokenCreds = new TokenCredentials(deviceIdentity, token, string.Empty, Option.None<string>(), Option.None<string>(), false);

            var deviceScopeIdentitiesCache = new Mock<IDeviceScopeIdentitiesCache>(MockBehavior.Strict);
            var deviceServiceIdentity = new ServiceIdentity(deviceIdentity.Id, "1234", new string[0], new ServiceAuthentication(ServiceAuthenticationType.CertificateAuthority), ServiceIdentityStatus.Disabled);
            deviceScopeIdentitiesCache.Setup(d => d.GetServiceIdentity(It.Is<string>(i => i == deviceIdentity.Id)))
                .ReturnsAsync(Option.Some(deviceServiceIdentity));
            deviceScopeIdentitiesCache.Setup(d => d.GetAuthChain(It.Is<string>(i => i == deviceIdentity.Id)))
                .ReturnsAsync(Option.Some(deviceIdentity.Id));

            var credentialsCache = new Mock<ICredentialsCache>(MockBehavior.Strict);
            credentialsCache.Setup(c => c.Get(deviceIdentity)).ReturnsAsync(Option.Some((IClientCredentials)tokenCreds));
            var metadataStore = new Mock<IMetadataStore>();
            metadataStore.Setup(m => m.GetMetadata(It.IsAny<string>())).ReturnsAsync(new ConnectionMetadata("dummyValue"));
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
                TimeSpan.FromSeconds(20),
                false,
                Option.None<IWebProxy>(),
                metadataStore.Object,
                scopeAuthenticationOnly: false,
                trackDeviceState: false,
                nestedEdgeEnabled: true);
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
            var tokenCreds = new TokenCredentials(deviceIdentity, token, string.Empty, Option.None<string>(), Option.None<string>(), false);

            var deviceScopeIdentitiesCache = new Mock<IDeviceScopeIdentitiesCache>(MockBehavior.Strict);
            deviceScopeIdentitiesCache.Setup(d => d.GetServiceIdentity(It.Is<string>(i => i == deviceIdentity.Id)))
                .ReturnsAsync(Option.None<ServiceIdentity>());
            deviceScopeIdentitiesCache.Setup(d => d.GetAuthChain(It.Is<string>(i => i == deviceIdentity.Id)))
                .ReturnsAsync(Option.Some(deviceIdentity.Id));

            var credentialsCache = new Mock<ICredentialsCache>(MockBehavior.Strict);
            credentialsCache.Setup(c => c.Get(deviceIdentity)).ReturnsAsync(Option.Some((IClientCredentials)tokenCreds));
            var metadataStore = new Mock<IMetadataStore>();
            metadataStore.Setup(m => m.GetMetadata(It.IsAny<string>())).ReturnsAsync(new ConnectionMetadata("dummyValue"));
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
                TimeSpan.FromSeconds(20),
                false,
                Option.None<IWebProxy>(),
                metadataStore.Object,
                scopeAuthenticationOnly: false,
                trackDeviceState: false,
                nestedEdgeEnabled: true);
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
        public void GetTransportSettingsTest(Option<UpstreamProtocol> upstreamProtocol, int connectionPoolSize, Option<IWebProxy> proxy, ITransportSettings[] expectedTransportSettingsList, bool useServerHeartbeat)
        {
            const string expectedAuthChain = "e3;e2;e1";
            ITransportSettings[] transportSettingsList = CloudConnectionProvider.GetTransportSettings(upstreamProtocol, connectionPoolSize, proxy, useServerHeartbeat, expectedAuthChain);

            Assert.NotNull(transportSettingsList);
            Assert.Equal(expectedTransportSettingsList.Length, transportSettingsList.Length);
            for (int i = 0; i < expectedTransportSettingsList.Length; i++)
            {
                ITransportSettings expectedTransportSettings = expectedTransportSettingsList[i];
                ITransportSettings transportSettings = transportSettingsList[i];

                Assert.Equal(expectedTransportSettings.GetType(), transportSettings.GetType());
                Assert.Equal(expectedTransportSettings.GetTransportType(), transportSettings.GetTransportType());

                // Check authchain via Reflection
                string authChain = (string)transportSettings.GetType()
                    .GetProperty("AuthenticationChain", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(transportSettings);

                Assert.Equal(authChain, expectedAuthChain);

                switch (expectedTransportSettings)
                {
                    case AmqpTransportSettings _:
                    {
                        var expected = (AmqpTransportSettings)expectedTransportSettings;
                        var actual = (AmqpTransportSettings)transportSettings;
                        Assert.True(expected.Equals(actual)); // AmqpTransportSettings impls Equals, but doesn't override Object.Equals

                        if (proxy == Option.None<IWebProxy>() || upstreamProtocol.Contains(UpstreamProtocol.Amqp))
                            {
                            Assert.Null(actual.Proxy);
                        }
                        else
                        {
                            Assert.True(actual.Proxy is WebProxy);
                            Assert.Equal(((WebProxy)expected.Proxy).Address, ((WebProxy)actual.Proxy).Address);
                        }

                        break;
                    }

                    case MqttTransportSettings _:
                    {
                        var expected = (MqttTransportSettings)expectedTransportSettings;
                        var actual = (MqttTransportSettings)transportSettings;

                        if (proxy == Option.None<IWebProxy>() || upstreamProtocol.Contains(UpstreamProtocol.Mqtt))
                        {
                            Assert.Null(actual.Proxy);
                        }
                        else
                        {
                            Assert.True(actual.Proxy is WebProxy);
                            Assert.Equal(((WebProxy)expected.Proxy).Address, ((WebProxy)actual.Proxy).Address);
                        }

                        break;
                    }
                }
            }
        }

        static IClientProvider GetMockDeviceClientProvider(bool openAsyncThrows = false)
        {
            var deviceClientProvider = new Mock<IClientProvider>();
            deviceClientProvider.Setup(dc => dc.Create(It.IsAny<IIdentity>(), It.IsAny<ITokenProvider>(), It.IsAny<ITransportSettings[]>(), Option.None<string>()))
                .Returns(() => GetMockDeviceClient(openAsyncThrows));
            return deviceClientProvider.Object;
        }

        static IClient GetMockDeviceClient(bool openAsyncThrows = false)
        {
            var deviceClient = new Mock<IClient>();
            deviceClient.SetupGet(dc => dc.IsActive).Returns(true);
            deviceClient.Setup(dc => dc.CloseAsync())
                .Callback(() => deviceClient.SetupGet(dc => dc.IsActive).Returns(false))
                .Returns(Task.FromResult(true));
            if (openAsyncThrows)
            {
                deviceClient.Setup(dc => dc.OpenAsync()).ThrowsAsync(new UnauthorizedException());
            }
            else
            {
                deviceClient.Setup(dc => dc.OpenAsync()).Returns(Task.CompletedTask);
            }

            return deviceClient.Object;
        }
    }
}
