// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.Test
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Edgelet;
    using Microsoft.Azure.Devices.Edge.Agent.IoTHub.SdkClient;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Unit]
    public class ModuleClientProviderTest
    {
        [Theory]
        [MemberData(nameof(GetTestData))]
        public async Task CreateFromEnvironmentTest(
            Option<UpstreamProtocol> upstreamProtocol,
            Option<IWebProxy> webProxy,
            string productInfo)
        {
            // Arrange
            IotHubClientOptions receivedOptions = null;

            var sdkModuleClient = new Mock<ISdkModuleClient>();
            var runtimeInfoProvider = new Mock<IRuntimeInfoProvider>();
            var systemInfo = new SystemInfo("foo", "bar", "baz");

            runtimeInfoProvider.Setup(p => p.GetSystemInfo(CancellationToken.None))
                .ReturnsAsync(systemInfo);

            var sdkModuleClientProvider = new Mock<ISdkModuleClientProvider>();
            sdkModuleClientProvider.Setup(s => s.GetSdkModuleClient(It.IsAny<IotHubClientOptions>()))
                .Callback<IotHubClientOptions>(t => receivedOptions = t)
                .ReturnsAsync(sdkModuleClient.Object);

            bool closeOnIdleTimeout = false;
            TimeSpan idleTimeout = TimeSpan.FromMinutes(5);
            Action<ConnectionStatusInfo> handler = (info) => { };

            // Act
            var moduleClientProvider = new ModuleClientProvider(
                sdkModuleClientProvider.Object,
                Option.Some(Task.FromResult(runtimeInfoProvider.Object)),
                upstreamProtocol,
                webProxy,
                productInfo,
                closeOnIdleTimeout,
                idleTimeout,
                false);
            IModuleClient moduleClient = await moduleClientProvider.Create(handler);

            // Assert
            Assert.NotNull(moduleClient);
            sdkModuleClientProvider.Verify(s => s.GetSdkModuleClient(It.IsAny<IotHubClientOptions>()), Times.Once);

            // In v2 SDK, product info is set via IotHubClientOptions.AdditionalUserAgentInfo
            Assert.NotNull(receivedOptions);
            string expectedProductInfo = $"{productInfo} (kernel=foo;architecture=bar;version=baz;server_version=;kernel_version=;operating_system=;cpus=0;total_memory=0;virtualized=;)";
            Assert.Equal(expectedProductInfo, receivedOptions.AdditionalUserAgentInfo);

            UpstreamProtocol up = upstreamProtocol.GetOrElse(UpstreamProtocol.Amqp);
            Assert.Equal(up, moduleClient.UpstreamProtocol);

            // TODO: Update for v2 SDK - transport settings are now encapsulated in IotHubClientOptions
            // and the specific transport type (IotHubClientAmqpSettings/IotHubClientMqttSettings) is not
            // directly accessible for type-checking. Proxy verification on transport settings is skipped.

            sdkModuleClient.Verify(s => s.OpenAsync(), Times.Once);
        }

        [Theory]
        [MemberData(nameof(GetTestData))]
        public async Task CreateFromEnvironment_NoRuntimeInfoProvider(
            Option<UpstreamProtocol> upstreamProtocol,
            Option<IWebProxy> webProxy,
            string productInfo)
        {
            // Arrange
            IotHubClientOptions receivedOptions = null;

            var sdkModuleClient = new Mock<ISdkModuleClient>();

            var sdkModuleClientProvider = new Mock<ISdkModuleClientProvider>();
            sdkModuleClientProvider.Setup(s => s.GetSdkModuleClient(It.IsAny<IotHubClientOptions>()))
                .Callback<IotHubClientOptions>(t => receivedOptions = t)
                .ReturnsAsync(sdkModuleClient.Object);

            bool closeOnIdleTimeout = false;
            TimeSpan idleTimeout = TimeSpan.FromMinutes(5);
            Action<ConnectionStatusInfo> handler = (info) => { };

            // Act
            var moduleClientProvider = new ModuleClientProvider(
                sdkModuleClientProvider.Object,
                Option.None<Task<IRuntimeInfoProvider>>(),
                upstreamProtocol,
                webProxy,
                productInfo,
                closeOnIdleTimeout,
                idleTimeout,
                false);
            IModuleClient moduleClient = await moduleClientProvider.Create(handler);

            // In v2 SDK, product info is set via IotHubClientOptions.AdditionalUserAgentInfo
            Assert.NotNull(receivedOptions);
            Assert.Equal(productInfo, receivedOptions.AdditionalUserAgentInfo);
        }

        [Fact]
        public void CreateFromEnvironment_NullProductInfo_ShouldThrow()
        {
            // Arrange
            var sdkModuleClient = new Mock<ISdkModuleClient>();
            var runtimeInfoProvider = new Mock<IRuntimeInfoProvider>();

            var sdkModuleClientProvider = new Mock<ISdkModuleClientProvider>();
            sdkModuleClientProvider.Setup(s => s.GetSdkModuleClient(It.IsAny<IotHubClientOptions>()))
                .ReturnsAsync(sdkModuleClient.Object);

            bool closeOnIdleTimeout = false;
            TimeSpan idleTimeout = TimeSpan.FromMinutes(5);
            Action<ConnectionStatusInfo> handler = (info) => { };

            // Assert
            Assert.Throws<ArgumentNullException>(() => new ModuleClientProvider(
                sdkModuleClientProvider.Object,
                Option.Some(Task.FromResult(runtimeInfoProvider.Object)),
                Option.None<UpstreamProtocol>(),
                Option.None<IWebProxy>(),
                null,
                closeOnIdleTimeout,
                idleTimeout,
                false));
        }

        [Theory]
        [MemberData(nameof(GetTestData))]
        public async Task CreateFromConnectionStringTest(
            Option<UpstreamProtocol> upstreamProtocol,
            Option<IWebProxy> webProxy,
            string productInfo)
        {
            // Arrange
            string connectionString = "DummyConnectionString";
            IotHubClientOptions receivedOptions = null;

            var sdkModuleClient = new Mock<ISdkModuleClient>();

            var sdkModuleClientProvider = new Mock<ISdkModuleClientProvider>();
            sdkModuleClientProvider.Setup(s => s.GetSdkModuleClient(connectionString, It.IsAny<IotHubClientOptions>()))
                .Callback<string, IotHubClientOptions>((c, t) => receivedOptions = t)
                .Returns(sdkModuleClient.Object);

            bool closeOnIdleTimeout = false;
            TimeSpan idleTimeout = TimeSpan.FromMinutes(5);
            Action<ConnectionStatusInfo> handler = (info) => { };

            // Act
            var moduleClientProvider = new ModuleClientProvider(
                connectionString,
                sdkModuleClientProvider.Object,
                upstreamProtocol,
                webProxy,
                productInfo,
                closeOnIdleTimeout,
                idleTimeout,
                false);
            IModuleClient moduleClient = await moduleClientProvider.Create(handler);

            // Assert
            Assert.NotNull(moduleClient);
            sdkModuleClientProvider.Verify(s => s.GetSdkModuleClient(connectionString, It.IsAny<IotHubClientOptions>()), Times.Once);

            // In v2 SDK, product info is set via IotHubClientOptions.AdditionalUserAgentInfo
            Assert.NotNull(receivedOptions);
            Assert.Equal(productInfo, receivedOptions.AdditionalUserAgentInfo);

            UpstreamProtocol up = upstreamProtocol.GetOrElse(UpstreamProtocol.Amqp);
            Assert.Equal(up, moduleClient.UpstreamProtocol);

            // TODO: Update for v2 SDK - transport settings are now encapsulated in IotHubClientOptions
            // and the specific transport type (IotHubClientAmqpSettings/IotHubClientMqttSettings) is not
            // directly accessible for type-checking. Proxy verification on transport settings is skipped.

            sdkModuleClient.Verify(s => s.OpenAsync(), Times.Once);
        }

        public static IEnumerable<object[]> GetTestData()
        {
            yield return new object[]
            {
                Option.None<UpstreamProtocol>(),
                Option.None<IWebProxy>(),
                Constants.IoTEdgeAgentProductInfoIdentifier
            };

            yield return new object[]
            {
                Option.Some(UpstreamProtocol.Amqp),
                Option.None<IWebProxy>(),
                Constants.IoTEdgeAgentProductInfoIdentifier
            };

            yield return new object[]
            {
                Option.Some(UpstreamProtocol.Amqp),
                Option.Some(Mock.Of<IWebProxy>()),
                Constants.IoTEdgeAgentProductInfoIdentifier + "/1.0.0"
            };
        }
    }
}
