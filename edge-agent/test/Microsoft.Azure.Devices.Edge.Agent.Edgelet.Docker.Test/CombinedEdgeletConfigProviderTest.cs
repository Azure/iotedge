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
            Assert.Throws<ArgumentNullException>(() => new CombinedEdgeletConfigProvider(new [] { new AuthConfig(), }, null));
            Assert.NotNull(new CombinedEdgeletConfigProvider(new [] { new AuthConfig(), }, new Uri("unix:///var/run/iotedgeworkload.sock")));
        }

        [Fact]
        public void TestVolMount()
        {
            // Arrange
            var runtimeInfo = new Mock<IRuntimeInfo<DockerRuntimeConfig>>();
            runtimeInfo.SetupGet(ri => ri.Config).Returns(new DockerRuntimeConfig("1.24", string.Empty));

            var module = new Mock<IModule<DockerConfig>>();
            module.SetupGet(m => m.Config).Returns(new DockerConfig("nginx:latest"));

             var provider = new CombinedEdgeletConfigProvider(new []{ new AuthConfig(), }, new Uri("unix:///var/run/iotedgedworkload.sock"));

             // Act
             CombinedDockerConfig config = provider.GetCombinedConfig(module.Object, runtimeInfo.Object);

             // Assert
             Assert.NotNull(config.CreateOptions);
             Assert.NotNull(config.CreateOptions.HostConfig);
             Assert.NotNull(config.CreateOptions.HostConfig.Binds);
             Assert.Equal(1, config.CreateOptions.HostConfig.Binds.Count);
             Assert.Equal("/var/run/iotedgedworkload.sock:/var/run/iotedgedworkload.sock", config.CreateOptions.HostConfig.Binds[0]);
        }

        [Fact]
        public void TestNoVolMountForNonUds()
        {
            // Arrange
            var runtimeInfo = new Mock<IRuntimeInfo<DockerRuntimeConfig>>();
            runtimeInfo.SetupGet(ri => ri.Config).Returns(new DockerRuntimeConfig("1.24", string.Empty));

            var module = new Mock<IModule<DockerConfig>>();
            module.SetupGet(m => m.Config).Returns(new DockerConfig("nginx:latest"));

            var provider = new CombinedEdgeletConfigProvider(new []{ new AuthConfig(), }, new Uri("http://localhost:2375/"));

            // Act
            CombinedDockerConfig config = provider.GetCombinedConfig(module.Object, runtimeInfo.Object);

            // Assert
            Assert.NotNull(config.CreateOptions);
            Assert.Null(config.CreateOptions.HostConfig);
        }
    }
}
