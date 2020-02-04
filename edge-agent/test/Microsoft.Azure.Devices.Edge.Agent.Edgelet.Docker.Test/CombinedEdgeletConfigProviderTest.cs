// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Edgelet.Docker.Test
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using global::Docker.DotNet.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Extensions.Configuration;
    using Moq;
    using Xunit;

    [Unit]
    public class CombinedEdgeletConfigProviderTest
    {
        [Fact]
        public void TestCreateValidation()
        {
            Assert.Throws<ArgumentNullException>(() => new CombinedEdgeletConfigProvider(new[] { new AuthConfig(), }, null));
            Assert.Throws<ArgumentNullException>(() => new CombinedEdgeletConfigProvider(null, Mock.Of<IConfigSource>()));
        }

        [Fact]
        public void TestVolMount()
        {
            // Arrange
            var runtimeInfo = new Mock<IRuntimeInfo<DockerRuntimeConfig>>();
            runtimeInfo.SetupGet(ri => ri.Config).Returns(new DockerRuntimeConfig("1.24", string.Empty));

            var module = new Mock<IModule<DockerConfig>>();
            module.SetupGet(m => m.Config).Returns(new DockerConfig("nginx:latest"));
            module.SetupGet(m => m.Name).Returns(Constants.EdgeAgentModuleName);

            var unixUris = new Dictionary<string, string>
            {
                { Constants.EdgeletWorkloadUriVariableName, "unix:///path/to/workload.sock" },
                { Constants.EdgeletManagementUriVariableName, "unix:///path/to/mgmt.sock" }
            };

            var windowsUris = new Dictionary<string, string>
            {
                { Constants.EdgeletWorkloadUriVariableName, "unix:///C:/path/to/workload/sock" },
                { Constants.EdgeletManagementUriVariableName, "unix:///C:/path/to/mgmt/sock" }
            };

            IConfigurationRoot configRoot = new ConfigurationBuilder().AddInMemoryCollection(
                RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? windowsUris : unixUris).Build();
            var configSource = Mock.Of<IConfigSource>(s => s.Configuration == configRoot);
            ICombinedConfigProvider<CombinedDockerConfig> provider = new CombinedEdgeletConfigProvider(new[] { new AuthConfig() }, configSource);

            // Act
            CombinedDockerConfig config = provider.GetCombinedConfig(module.Object, runtimeInfo.Object);

            // Assert
            Assert.NotNull(config.CreateOptions);
            Assert.NotNull(config.CreateOptions.HostConfig);
            Assert.NotNull(config.CreateOptions.HostConfig.Binds);
            Assert.Equal(2, config.CreateOptions.HostConfig.Binds.Count);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert.Equal("C:\\path\\to\\workload:C:\\path\\to\\workload", config.CreateOptions.HostConfig.Binds[0]);
                Assert.Equal("C:\\path\\to\\mgmt:C:\\path\\to\\mgmt", config.CreateOptions.HostConfig.Binds[1]);
            }
            else
            {
                Assert.Equal("/path/to/workload.sock:/path/to/workload.sock", config.CreateOptions.HostConfig.Binds[0]);
                Assert.Equal("/path/to/mgmt.sock:/path/to/mgmt.sock", config.CreateOptions.HostConfig.Binds[1]);
            }
        }

        [Fact]
        public void TestNoVolMountForNonUds()
        {
            // Arrange
            var runtimeInfo = new Mock<IRuntimeInfo<DockerRuntimeConfig>>();
            runtimeInfo.SetupGet(ri => ri.Config).Returns(new DockerRuntimeConfig("1.24", string.Empty));

            var module = new Mock<IModule<DockerConfig>>();
            module.SetupGet(m => m.Config).Returns(new DockerConfig("nginx:latest"));
            module.SetupGet(m => m.Name).Returns(Constants.EdgeAgentModuleName);

            IConfigurationRoot configRoot = new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string>
                {
                    { Constants.EdgeletWorkloadUriVariableName, "http://localhost:2375/" },
                    { Constants.EdgeletManagementUriVariableName, "http://localhost:2376/" }
                }).Build();
            var configSource = Mock.Of<IConfigSource>(s => s.Configuration == configRoot);
            ICombinedConfigProvider<CombinedDockerConfig> provider = new CombinedEdgeletConfigProvider(new[] { new AuthConfig() }, configSource);

            // Act
            CombinedDockerConfig config = provider.GetCombinedConfig(module.Object, runtimeInfo.Object);

            // Assert
            Assert.NotNull(config.CreateOptions);
            Assert.Null(config.CreateOptions.HostConfig);
        }

        [Fact]
        public void InjectNetworkAliasTest()
        {
            // Arrange
            var runtimeInfo = new Mock<IRuntimeInfo<DockerRuntimeConfig>>();
            runtimeInfo.SetupGet(ri => ri.Config).Returns(new DockerRuntimeConfig("1.24", string.Empty));

            var module = new Mock<IModule<DockerConfig>>();
            module.SetupGet(m => m.Config).Returns(new DockerConfig("nginx:latest"));
            module.SetupGet(m => m.Name).Returns("mod1");

            IConfigurationRoot configRoot = new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string>
                {
                    { Constants.EdgeletWorkloadUriVariableName, "unix:///var/run/iotedgedworkload.sock" },
                    { Constants.EdgeletManagementUriVariableName, "unix:///var/run/iotedgedmgmt.sock" },
                    { Constants.NetworkIdKey, "testnetwork1" },
                    { Constants.EdgeDeviceHostNameKey, "edhk1" }
                }).Build();
            var configSource = Mock.Of<IConfigSource>(s => s.Configuration == configRoot);

            ICombinedConfigProvider<CombinedDockerConfig> provider = new CombinedEdgeletConfigProvider(new[] { new AuthConfig() }, configSource);

            // Act
            CombinedDockerConfig config = provider.GetCombinedConfig(module.Object, runtimeInfo.Object);

            // Assert
            Assert.NotNull(config.CreateOptions);
            Assert.NotNull(config.CreateOptions.NetworkingConfig);
            Assert.NotNull(config.CreateOptions.NetworkingConfig.EndpointsConfig);
            Assert.NotNull(config.CreateOptions.NetworkingConfig.EndpointsConfig["testnetwork1"]);
            Assert.Null(config.CreateOptions.NetworkingConfig.EndpointsConfig["testnetwork1"].Aliases);
        }

        [Fact]
        public void InjectNetworkAlias_EdgeHubTest()
        {
            // Arrange
            var runtimeInfo = new Mock<IRuntimeInfo<DockerRuntimeConfig>>();
            runtimeInfo.SetupGet(ri => ri.Config).Returns(new DockerRuntimeConfig("1.24", string.Empty));

            var module = new Mock<IModule<DockerConfig>>();
            module.SetupGet(m => m.Config).Returns(new DockerConfig("nginx:latest"));
            module.SetupGet(m => m.Name).Returns(Constants.EdgeHubModuleName);

            IConfigurationRoot configRoot = new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string>
                {
                    { Constants.EdgeletWorkloadUriVariableName, "unix:///var/run/iotedgedworkload.sock" },
                    { Constants.EdgeletManagementUriVariableName, "unix:///var/run/iotedgedmgmt.sock" },
                    { Constants.NetworkIdKey, "testnetwork1" },
                    { Constants.EdgeDeviceHostNameKey, "edhk1" }
                }).Build();
            var configSource = Mock.Of<IConfigSource>(s => s.Configuration == configRoot);

            ICombinedConfigProvider<CombinedDockerConfig> provider = new CombinedEdgeletConfigProvider(new[] { new AuthConfig() }, configSource);

            // Act
            CombinedDockerConfig config = provider.GetCombinedConfig(module.Object, runtimeInfo.Object);

            // Assert
            Assert.NotNull(config.CreateOptions);
            Assert.NotNull(config.CreateOptions.NetworkingConfig);
            Assert.NotNull(config.CreateOptions.NetworkingConfig.EndpointsConfig);
            Assert.NotNull(config.CreateOptions.NetworkingConfig.EndpointsConfig["testnetwork1"]);
            Assert.Equal("edhk1", config.CreateOptions.NetworkingConfig.EndpointsConfig["testnetwork1"].Aliases[0]);
        }

        [Fact]
        public void InjectNetworkAlias_HostNetworkTest()
        {
            // Arrange
            var runtimeInfo = new Mock<IRuntimeInfo<DockerRuntimeConfig>>();
            runtimeInfo.SetupGet(ri => ri.Config).Returns(new DockerRuntimeConfig("1.24", string.Empty));

            string hostNetworkCreateOptions = "{\"NetworkingConfig\":{\"EndpointsConfig\":{\"host\":{}}},\"HostConfig\":{\"NetworkMode\":\"host\"}}";
            var module = new Mock<IModule<DockerConfig>>();
            module.SetupGet(m => m.Config).Returns(new DockerConfig("nginx:latest", hostNetworkCreateOptions));
            module.SetupGet(m => m.Name).Returns("mod1");

            IConfigurationRoot configRoot = new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string>
                {
                    { Constants.EdgeletWorkloadUriVariableName, "unix:///var/run/iotedgedworkload.sock" },
                    { Constants.EdgeletManagementUriVariableName, "unix:///var/run/iotedgedmgmt.sock" },
                    { Constants.NetworkIdKey, "testnetwork1" },
                    { Constants.EdgeDeviceHostNameKey, "edhk1" }
                }).Build();
            var configSource = Mock.Of<IConfigSource>(s => s.Configuration == configRoot);

            ICombinedConfigProvider<CombinedDockerConfig> provider = new CombinedEdgeletConfigProvider(new[] { new AuthConfig() }, configSource);

            // Act
            CombinedDockerConfig config = provider.GetCombinedConfig(module.Object, runtimeInfo.Object);

            // Assert
            Assert.NotNull(config.CreateOptions);
            Assert.NotNull(config.CreateOptions.NetworkingConfig);
            Assert.NotNull(config.CreateOptions.NetworkingConfig.EndpointsConfig);
            Assert.False(config.CreateOptions.NetworkingConfig.EndpointsConfig.ContainsKey("testnetwork1"));
            Assert.NotNull(config.CreateOptions.NetworkingConfig.EndpointsConfig["host"]);
            Assert.Null(config.CreateOptions.NetworkingConfig.EndpointsConfig["host"].Aliases);
            Assert.Equal("host", config.CreateOptions.HostConfig.NetworkMode);
        }

        [Fact]
        public void ExtensionDataFields()
        {
            // Arrange
            var runtimeInfo = new Mock<IRuntimeInfo<DockerRuntimeConfig>>();
            runtimeInfo.SetupGet(ri => ri.Config).Returns(new DockerRuntimeConfig("1.24", string.Empty));

            string createOptions = "{\"HostConfig\":{\"Devices\":[],\"DeviceRequests\":[{\"Driver\":\"\",\"Count\":-1,\"DeviceIDs\":null,\"Capabilities\":[[\"gpu\"]],\"Options\":{}}]}}";
            var module = new Mock<IModule<DockerConfig>>();
            module.SetupGet(m => m.Config).Returns(new DockerConfig("nginx:latest", createOptions));
            module.SetupGet(m => m.Name).Returns("mod1");

            IConfigurationRoot configRoot = new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string>
                {
                    { Constants.EdgeletWorkloadUriVariableName, "unix:///var/run/iotedgedworkload.sock" },
                    { Constants.EdgeletManagementUriVariableName, "unix:///var/run/iotedgedmgmt.sock" },
                    { Constants.NetworkIdKey, "testnetwork1" },
                    { Constants.EdgeDeviceHostNameKey, "edhk1" }
                }).Build();
            var configSource = Mock.Of<IConfigSource>(s => s.Configuration == configRoot);

            ICombinedConfigProvider<CombinedDockerConfig> provider = new CombinedEdgeletConfigProvider(new[] { new AuthConfig() }, configSource);

            // Act
            CombinedDockerConfig config = provider.GetCombinedConfig(module.Object, runtimeInfo.Object);

            // Assert
            Assert.NotNull(config.CreateOptions);
            Assert.NotNull(config.CreateOptions.HostConfig);

            var otherProperties = config.CreateOptions.HostConfig.OtherProperties;
            Assert.NotNull(otherProperties);

            var reserializedHostConfig = Newtonsoft.Json.JsonConvert.SerializeObject(config.CreateOptions.HostConfig);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert.Equal(
                    "{\"Binds\":[\"\\\\var\\\\run:\\\\var\\\\run\"],\"Devices\":[],\"DeviceRequests\":[{\"Driver\":\"\",\"Count\":-1,\"DeviceIDs\":null,\"Capabilities\":[[\"gpu\"]],\"Options\":{}}]}",
                    reserializedHostConfig);
            }
            else
            {
                Assert.Equal(
                    "{\"Binds\":[\"/var/run/iotedgedworkload.sock:/var/run/iotedgedworkload.sock\"],\"Devices\":[],\"DeviceRequests\":[{\"Driver\":\"\",\"Count\":-1,\"DeviceIDs\":null,\"Capabilities\":[[\"gpu\"]],\"Options\":{}}]}",
                    reserializedHostConfig);
            }
        }
    }
}
