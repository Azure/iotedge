// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Test
{
    using System;
    using System.Linq;
    using System.Runtime.InteropServices;
    using global::Docker.DotNet.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
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
            Assert.Throws<ArgumentNullException>(() => new CombinedKubernetesConfigProvider(null, new Uri("http://workload"), new Uri("http://management"), false));
            Assert.Throws<ArgumentNullException>(() => new CombinedKubernetesConfigProvider(new[] { new AuthConfig(), }, null, new Uri("http://management"), false));
            Assert.Throws<ArgumentNullException>(() => new CombinedKubernetesConfigProvider(new[] { new AuthConfig(), }, new Uri("http://workload"), null, false));
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

            CombinedKubernetesConfigProvider provider = new CombinedKubernetesConfigProvider(new[] { new AuthConfig() }, workloadUri, managementUri, false);

            // Act
            CombinedKubernetesConfig config = provider.GetCombinedConfig(module.Object, runtimeInfo.Object);

            // Assert
            Assert.NotNull(config.CreateOptions);
            Assert.True(config.CreateOptions.HostConfig.HasValue);
            config.CreateOptions.HostConfig.ForEach(hostConfig => Assert.NotNull(hostConfig.Binds));
            config.CreateOptions.HostConfig.ForEach(hostConfig => Assert.Equal(2, hostConfig.Binds.Count));

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                config.CreateOptions.HostConfig.ForEach(hostConfig => Assert.Equal(@"C:\path\to\workload:C:\path\to\workload", hostConfig.Binds[0]));
                config.CreateOptions.HostConfig.ForEach(hostConfig => Assert.Equal(@"C:\path\to\mgmt:C:\path\to\mgmt", hostConfig.Binds[1]));
            }
            else
            {
                config.CreateOptions.HostConfig.ForEach(hostConfig => Assert.Equal("/path/to/workload.sock:/path/to/workload.sock", hostConfig.Binds[0]));
                config.CreateOptions.HostConfig.ForEach(hostConfig => Assert.Equal("/path/to/mgmt.sock:/path/to/mgmt.sock", hostConfig.Binds[1]));
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

            CombinedKubernetesConfigProvider provider = new CombinedKubernetesConfigProvider(new[] { new AuthConfig() }, new Uri("http://localhost:2375/"), new Uri("http://localhost:2376/"), false);

            // Act
            CombinedKubernetesConfig config = provider.GetCombinedConfig(module.Object, runtimeInfo.Object);

            // Assert
            Assert.NotNull(config.CreateOptions);
            Assert.False(config.CreateOptions.HostConfig.HasValue);
        }

        [Fact]
        public void IgnoresKubernetesCreateOptionsWhenExperimentalDisabled()
        {
            // Arrange
            var runtimeInfo = new Mock<IRuntimeInfo<DockerRuntimeConfig>>();
            runtimeInfo.SetupGet(ri => ri.Config).Returns(new DockerRuntimeConfig("1.24", string.Empty));

            var module = new Mock<IModule<DockerConfig>>();
            module.SetupGet(m => m.Config).Returns(new DockerConfig("nginx:latest", ExperimentalCreateOptions));
            module.SetupGet(m => m.Name).Returns("mod1");

            CombinedKubernetesConfigProvider provider = new CombinedKubernetesConfigProvider(new[] { new AuthConfig() }, new Uri("unix:///var/run/iotedgedworkload.sock"), new Uri("unix:///var/run/iotedgedmgmt.sock"), false);

            // Act
            CombinedKubernetesConfig config = provider.GetCombinedConfig(module.Object, runtimeInfo.Object);

            // Assert
            Assert.False(config.CreateOptions.Volumes.HasValue);
            Assert.False(config.CreateOptions.NodeSelector.HasValue);
            Assert.False(config.CreateOptions.Resources.HasValue);
        }

        [Fact]
        public void ParsesKubernetesCreateOptionsWhenExperimentalEnabled()
        {
            // Arrange
            var runtimeInfo = new Mock<IRuntimeInfo<DockerRuntimeConfig>>();
            runtimeInfo.SetupGet(ri => ri.Config).Returns(new DockerRuntimeConfig("1.24", string.Empty));

            var module = new Mock<IModule<DockerConfig>>();
            module.SetupGet(m => m.Config).Returns(new DockerConfig("nginx:latest", ExperimentalCreateOptions));
            module.SetupGet(m => m.Name).Returns("mod1");

            CombinedKubernetesConfigProvider provider = new CombinedKubernetesConfigProvider(new[] { new AuthConfig() }, new Uri("unix:///var/run/iotedgedworkload.sock"), new Uri("unix:///var/run/iotedgedmgmt.sock"), true);

            // Act
            CombinedKubernetesConfig config = provider.GetCombinedConfig(module.Object, runtimeInfo.Object);

            // Assert
            Assert.True(config.CreateOptions.Volumes.HasValue);
            config.CreateOptions.Volumes.ForEach(volumes => Assert.Equal(1, volumes.Count));
            config.CreateOptions.Volumes.ForEach(volumes => Assert.NotNull(volumes.First()));

            Assert.True(config.CreateOptions.NodeSelector.HasValue);
            config.CreateOptions.NodeSelector.ForEach(selector => Assert.Equal(2, selector.Count));

            Assert.True(config.CreateOptions.Resources.HasValue);
            config.CreateOptions.Resources.ForEach(resources => Assert.Equal(3, resources.Limits.Count));
            config.CreateOptions.Resources.ForEach(resources => Assert.Equal(3, resources.Requests.Count));
        }

        const string ExperimentalCreateOptions =
            @"{
  ""k8s-experimental"": {
    ""volumes"": [
      {
        ""volume"": {
          ""name"": ""ModuleA"",
          ""configMap"": {
            ""optional"": ""true"",
            ""defaultMode"": 420,
            ""items"": [
              {
                ""key"": ""config-file"",
                ""path"": ""config.yaml"",
                ""mode"": 420
              }
            ],
            ""name"": ""module-config""
          }
        },
        ""volumeMounts"": [
          {
            ""name"": ""module-config"",
            ""mountPath"": ""/etc/module/config.yaml"",
            ""mountPropagation"": ""None"",
            ""readOnly"": ""true"",
            ""subPath"": """"
          }
        ]
      }
    ],
    ""resources"": {
      ""limits"": {
        ""memory"": ""128Mi"",
        ""cpu"": ""500m"",
        ""hardware-vendor.example/foo"": 2
      },
      ""requests"": {
        ""memory"": ""64Mi"",
        ""cpu"": ""250m"",
        ""hardware-vendor.example/foo"": 2
      }
    },
    ""nodeSelector"": {
      ""disktype"": ""ssd"",
      ""gpu"": ""true""
    }
  }
}";

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

            CombinedKubernetesConfigProvider provider = new CombinedKubernetesConfigProvider(new[] { authConfig }, new Uri("unix:///var/run/iotedgedworkload.sock"), new Uri("unix:///var/run/iotedgedmgmt.sock"), false);

            // Act
            CombinedKubernetesConfig config = provider.GetCombinedConfig(module.Object, runtimeInfo.Object);

            // Assert
            Assert.True(config.ImagePullSecret.HasValue);
            config.ImagePullSecret.ForEach(secret => Assert.Equal("user-docker.io", secret.Name));
        }
    }
}
