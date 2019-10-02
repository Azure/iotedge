// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Test
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using global::Docker.DotNet.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;
    using CoreConstants = Microsoft.Azure.Devices.Edge.Agent.Core.Constants;

    [Unit]
    public class CombinedKubernetesConfigProviderTest
    {
        [Fact]
        public void TestCreateValidation()
        {
            Assert.Throws<ArgumentNullException>(() => new CombinedKubernetesConfigProvider(null, "hostname", "network", new Uri("http://workload"), new Uri("http://management"), false));
            Assert.Throws<ArgumentNullException>(() => new CombinedKubernetesConfigProvider(new[] { new AuthConfig(), }, null, "network", new Uri("http://workload"), new Uri("http://management"), false));
            Assert.Throws<ArgumentNullException>(() => new CombinedKubernetesConfigProvider(new[] { new AuthConfig(), }, "hostname", null, new Uri("http://workload"), new Uri("http://management"), false));
            Assert.Throws<ArgumentNullException>(() => new CombinedKubernetesConfigProvider(new[] { new AuthConfig(), }, "hostname", "network", null, new Uri("http://management"), false));
            Assert.Throws<ArgumentNullException>(() => new CombinedKubernetesConfigProvider(new[] { new AuthConfig(), }, "hostname", "network", new Uri("http://workload"), null, false));
        }

        [Fact]
        public void TestVolMount()
        {
            // Arrange
            var runtimeInfo = new Mock<IRuntimeInfo<DockerRuntimeConfig>>();
            runtimeInfo.SetupGet(ri => ri.Config).Returns(new DockerRuntimeConfig("1.24", string.Empty));

            var module = new Mock<IModule<DockerConfig>>();
            module.SetupGet(m => m.Config).Returns(new DockerConfig("nginx:latest"));
            module.SetupGet(m => m.Name).Returns(CoreConstants.EdgeAgentModuleName);

            (Uri workloadUri, Uri managementUri) = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? (new Uri("unix:///C:/path/to/workload/sock"), new Uri("unix:///C:/path/to/mgmt/sock"))
                : (new Uri("unix:///path/to/workload.sock"), new Uri("unix:///path/to/mgmt.sock"));

            CombinedKubernetesConfigProvider provider = new CombinedKubernetesConfigProvider(new[] { new AuthConfig() }, "hostname", "network", workloadUri, managementUri, false);

            // Act
            CombinedKubernetesConfig config = ((ICombinedConfigProvider<CombinedKubernetesConfig>)provider).GetCombinedConfig(module.Object, runtimeInfo.Object);

            // Assert
            Assert.NotNull(config.CreateOptions);
            Assert.NotNull(config.CreateOptions.HostConfig);
            Assert.NotNull(config.CreateOptions.HostConfig.Binds);
            Assert.Equal(2, config.CreateOptions.HostConfig.Binds.Count);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert.Equal(@"C:\path\to\workload:C:\path\to\workload", config.CreateOptions.HostConfig.Binds[0]);
                Assert.Equal(@"C:\path\to\mgmt:C:\path\to\mgmt", config.CreateOptions.HostConfig.Binds[1]);
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
            module.SetupGet(m => m.Name).Returns(CoreConstants.EdgeAgentModuleName);

            CombinedKubernetesConfigProvider provider = new CombinedKubernetesConfigProvider(new[] { new AuthConfig() }, "hostname", "network", new Uri("http://localhost:2375/"), new Uri("http://localhost:2376/"), false);

            // Act
            CombinedKubernetesConfig config = ((ICombinedConfigProvider<CombinedKubernetesConfig>)provider).GetCombinedConfig(module.Object, runtimeInfo.Object);

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

            CombinedKubernetesConfigProvider provider = new CombinedKubernetesConfigProvider(new[] { new AuthConfig(), }, "edhk1", "testnetwork1", new Uri("unix:///var/run/iotedgedworkload.sock"), new Uri("unix:///var/run/iotedgedmgmt.sock"), false);

            // Act
            CombinedKubernetesConfig config = ((ICombinedConfigProvider<CombinedKubernetesConfig>)provider).GetCombinedConfig(module.Object, runtimeInfo.Object);

            // Assert
            Assert.NotNull(config.CreateOptions);
            Assert.NotNull(config.CreateOptions.NetworkingConfig);
            Assert.NotNull(config.CreateOptions.NetworkingConfig.EndpointsConfig);
            Assert.NotNull(config.CreateOptions.NetworkingConfig.EndpointsConfig["testnetwork1"]);
            Assert.Null(config.CreateOptions.NetworkingConfig.EndpointsConfig["testnetwork1"].Aliases);
        }

        [Fact]
        public void InjectNetworkAliasEdgeHubTest()
        {
            // Arrange
            var runtimeInfo = new Mock<IRuntimeInfo<DockerRuntimeConfig>>();
            runtimeInfo.SetupGet(ri => ri.Config).Returns(new DockerRuntimeConfig("1.24", string.Empty));

            var module = new Mock<IModule<DockerConfig>>();
            module.SetupGet(m => m.Config).Returns(new DockerConfig("nginx:latest"));
            module.SetupGet(m => m.Name).Returns(CoreConstants.EdgeHubModuleName);

            CombinedKubernetesConfigProvider provider = new CombinedKubernetesConfigProvider(new[] { new AuthConfig(), }, "edhk1", "testnetwork1", new Uri("http://workload"), new Uri("http://management"), false);

            // Act
            CombinedKubernetesConfig config = ((ICombinedConfigProvider<CombinedKubernetesConfig>)provider).GetCombinedConfig(module.Object, runtimeInfo.Object);

            // Assert
            Assert.NotNull(config.CreateOptions);
            Assert.NotNull(config.CreateOptions.NetworkingConfig);
            Assert.NotNull(config.CreateOptions.NetworkingConfig.EndpointsConfig);
            Assert.NotNull(config.CreateOptions.NetworkingConfig.EndpointsConfig["testnetwork1"]);
            Assert.Equal("edhk1", config.CreateOptions.NetworkingConfig.EndpointsConfig["testnetwork1"].Aliases[0]);
        }

        [Fact]
        public void InjectNetworkAliasHostNetworkTest()
        {
            // Arrange
            var runtimeInfo = new Mock<IRuntimeInfo<DockerRuntimeConfig>>();
            runtimeInfo.SetupGet(ri => ri.Config).Returns(new DockerRuntimeConfig("1.24", string.Empty));

            string hostNetworkCreateOptions = "{\"NetworkingConfig\":{\"EndpointsConfig\":{\"host\":{}}},\"HostConfig\":{\"NetworkMode\":\"host\"}}";
            var module = new Mock<IModule<DockerConfig>>();
            module.SetupGet(m => m.Config).Returns(new DockerConfig("nginx:latest", hostNetworkCreateOptions));
            module.SetupGet(m => m.Name).Returns("mod1");

            CombinedKubernetesConfigProvider provider = new CombinedKubernetesConfigProvider(new[] { new AuthConfig(), }, "edhk1", "testnetwork1", new Uri("unix:///var/run/iotedgedworkload.sock"), new Uri("unix:///var/run/iotedgedmgmt.sock"), false);

            // Act
            CombinedKubernetesConfig config = ((ICombinedConfigProvider<CombinedKubernetesConfig>)provider).GetCombinedConfig(module.Object, runtimeInfo.Object);

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
        public void IgnoresKubernetesCreateOptionsWhenExperimentalDisabled()
        {
            // Arrange
            var runtimeInfo = new Mock<IRuntimeInfo<DockerRuntimeConfig>>();
            runtimeInfo.SetupGet(ri => ri.Config).Returns(new DockerRuntimeConfig("1.24", string.Empty));

            var module = new Mock<IModule<DockerConfig>>();
            string createOptions = "{ \"k8s-experimental\": { nodeSelector: { disktype: \"ssd\" } } }";
            module.SetupGet(m => m.Config).Returns(new DockerConfig("nginx:latest", createOptions));
            module.SetupGet(m => m.Name).Returns("mod1");

            CombinedKubernetesConfigProvider provider = new CombinedKubernetesConfigProvider(new[] { new AuthConfig() }, "edhk1", "testnetwork1", new Uri("unix:///var/run/iotedgedworkload.sock"), new Uri("unix:///var/run/iotedgedmgmt.sock"), false);

            // Act
            CombinedKubernetesConfig config = ((ICombinedConfigProvider<CombinedKubernetesConfig>)provider).GetCombinedConfig(module.Object, runtimeInfo.Object);

            // Assert
            Assert.False(config.CreateOptions.NodeSelector.HasValue);
        }

        [Fact]
        public void ParsesKubernetesCreateOptionsWhenExperimentalEnabled()
        {
            // Arrange
            var runtimeInfo = new Mock<IRuntimeInfo<DockerRuntimeConfig>>();
            runtimeInfo.SetupGet(ri => ri.Config).Returns(new DockerRuntimeConfig("1.24", string.Empty));

            var module = new Mock<IModule<DockerConfig>>();
            string createOptions = "{ \"k8s-experimental\": { nodeSelector: { disktype: \"ssd\" } } }";
            module.SetupGet(m => m.Config).Returns(new DockerConfig("nginx:latest", createOptions));
            module.SetupGet(m => m.Name).Returns("mod1");

            CombinedKubernetesConfigProvider provider = new CombinedKubernetesConfigProvider(new[] { new AuthConfig() }, "edhk1", "testnetwork1", new Uri("unix:///var/run/iotedgedworkload.sock"), new Uri("unix:///var/run/iotedgedmgmt.sock"), true);

            // Act
            CombinedKubernetesConfig config = ((ICombinedConfigProvider<CombinedKubernetesConfig>)provider).GetCombinedConfig(module.Object, runtimeInfo.Object);

            // Assert
            Assert.True(config.CreateOptions.NodeSelector.HasValue);
            config.CreateOptions.NodeSelector.ForEach(selector => Assert.Equal(new Dictionary<string, string> { ["disktype"] = "ssd" }, selector, new DictionaryComparer<string, string>()));
        }

        [Fact]
        public void MakesKubernetesAwareAuthConfig()
        {
            // Arrange
            var runtimeInfo = new Mock<IRuntimeInfo<DockerRuntimeConfig>>();
            runtimeInfo.SetupGet(ri => ri.Config).Returns(new DockerRuntimeConfig("1.24", string.Empty));

            var module = new Mock<IModule<DockerConfig>>();
            module.SetupGet(m => m.Config).Returns(new DockerConfig("docker.io/nginx:latest", (string)null));
            module.SetupGet(m => m.Name).Returns("mod1");

            var authConfig = new AuthConfig { Username = "user", Password = "password", ServerAddress = "docker.io" };

            CombinedKubernetesConfigProvider provider = new CombinedKubernetesConfigProvider(new[] { authConfig }, "edhk1", "testnetwork1", new Uri("unix:///var/run/iotedgedworkload.sock"), new Uri("unix:///var/run/iotedgedmgmt.sock"), false);

            // Act
            CombinedKubernetesConfig config = ((ICombinedConfigProvider<CombinedKubernetesConfig>)provider).GetCombinedConfig(module.Object, runtimeInfo.Object);

            // Assert
            Assert.True(config.AuthConfig.HasValue);
            config.AuthConfig.ForEach(auth => Assert.Equal("user-docker.io", auth.Name));
        }
    }
}
