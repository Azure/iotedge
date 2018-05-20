namespace Microsoft.Azure.Devices.Edge.Agent.Edgelet.Docker.Test
{
    using System;
    using global::Docker.DotNet.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Moq;
    using Xunit;

    public class CombinedEdgeletConfigProviderTest
    {
        [Fact]
        public void TestCreateValidation()
        {
            Assert.Throws<ArgumentNullException>(() => new CombinedEdgeletConfigProvider(new[] { new AuthConfig(), }, null, new Uri("http://localhost:5000")));
            Assert.Throws<ArgumentNullException>(() => new CombinedEdgeletConfigProvider(new[] { new AuthConfig(), }, new Uri("http://localhost:5000"), null));
            Assert.NotNull(new CombinedEdgeletConfigProvider(new[] { new AuthConfig(), }, new Uri("unix:///var/run/iotedgeworkload.sock"), new Uri("unix:///var/run/iotedgemgmt.sock")));
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

            var provider = new CombinedEdgeletConfigProvider(
                new[] { new AuthConfig(), },
                new Uri("unix:///var/run/iotedgedworkload.sock"),
                new Uri("unix:///var/run/iotedgedmgmt.sock"));

            // Act
            CombinedDockerConfig config = provider.GetCombinedConfig(module.Object, runtimeInfo.Object);

            // Assert
            Assert.NotNull(config.CreateOptions);
            Assert.NotNull(config.CreateOptions.HostConfig);
            Assert.NotNull(config.CreateOptions.HostConfig.Binds);
            Assert.Equal(2, config.CreateOptions.HostConfig.Binds.Count);
            Assert.Equal("/var/run/iotedgedworkload.sock:/var/run/iotedgedworkload.sock", config.CreateOptions.HostConfig.Binds[0]);
            Assert.Equal("/var/run/iotedgedmgmt.sock:/var/run/iotedgedmgmt.sock", config.CreateOptions.HostConfig.Binds[1]);
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

            var provider = new CombinedEdgeletConfigProvider(
                new[] { new AuthConfig(), },
                new Uri("http://localhost:2375/"),
                new Uri("http://localhost:2376/"));

            // Act
            CombinedDockerConfig config = provider.GetCombinedConfig(module.Object, runtimeInfo.Object);

            // Assert
            Assert.NotNull(config.CreateOptions);
            Assert.Null(config.CreateOptions.HostConfig);
        }
    }
}
