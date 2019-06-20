// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Test
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Xunit;

    public class EdgeHubDockerRuntimeModuleTest
    {
        [Fact]
        [Unit]
        public void TestJsonSerialize()
        {
            // Arrange
            var module = new EdgeHubDockerRuntimeModule(
                ModuleStatus.Running,
                RestartPolicy.Always,
                new DockerConfig("edg0eHubImage:latest"),
                0,
                string.Empty,
                DateTime.MinValue,
                DateTime.MinValue,
                0,
                DateTime.MinValue,
                ModuleStatus.Running,
                ImagePullPolicy.Never,
                new ConfigurationInfo("1"),
                new Dictionary<string, EnvVal>());

            // Act
            JToken json = JToken.Parse(JsonConvert.SerializeObject(module));

            // Assert
            JToken expected = JToken.Parse(
                @"
{
  ""status"": ""running"",
  ""restartPolicy"": ""always"",
  ""imagePullPolicy"": ""never"",
  ""exitCode"": 0,
  ""statusDescription"": """",
  ""lastStartTimeUtc"": ""0001-01-01T00:00:00"",
  ""lastExitTimeUtc"": ""0001-01-01T00:00:00"",
  ""restartCount"": 0,
  ""lastRestartTimeUtc"": ""0001-01-01T00:00:00"",
  ""runtimeStatus"": ""running"",
  ""type"": ""docker"",
  ""settings"": {
    ""image"": ""edg0eHubImage:latest"",
    ""createOptions"": ""{}""
  },
  ""env"": {}
}
            ");

            Assert.True(JToken.DeepEquals(expected, json));
        }

        [Fact]
        [Unit]
        public void TestJsonDeerialize()
        {
            // Arrange
            string json = @"
{
  ""status"": ""running"",
  ""restartPolicy"": ""always"",
  ""imagePullPolicy"": ""never"",
  ""exitCode"": 0,
  ""statusDescription"": """",
  ""lastStartTimeUtc"": ""0001-01-01T00:00:00"",
  ""lastExitTimeUtc"": ""0001-01-01T00:00:00"",
  ""restartCount"": 0,
  ""lastRestartTimeUtc"": ""0001-01-01T00:00:00"",
  ""runtimeStatus"": ""running"",
  ""type"": ""docker"",
  ""settings"": {
    ""image"": ""edg0eHubImage"",
    ""createOptions"": ""{}""
  }
}";

            // Act
            var actual = JsonConvert.DeserializeObject<EdgeHubDockerRuntimeModule>(json);

            // Assert
            var expected = new EdgeHubDockerRuntimeModule(
                ModuleStatus.Running,
                RestartPolicy.Always,
                new DockerConfig("edg0eHubImage"),
                0,
                string.Empty,
                DateTime.MinValue,
                DateTime.MinValue,
                0,
                DateTime.MinValue,
                ModuleStatus.Running,
                ImagePullPolicy.Never,
                null,
                new Dictionary<string, EnvVal>());

            Assert.Equal(expected, actual);
        }

        [Fact]
        [Unit]
        public void EqualsTest()
        {
            // Arrange
            string image = "repo/microsoft/azureiotedge-hub:002";
            var edgeHubDockerModule = new EdgeHubDockerModule(
                "docker",
                ModuleStatus.Running,
                RestartPolicy.Always,
                new DockerConfig(image),
                ImagePullPolicy.OnCreate,
                new ConfigurationInfo("1"),
                new Dictionary<string, EnvVal>());

            var edgeHubDockerRuntimeModule = new EdgeHubDockerRuntimeModule(
                ModuleStatus.Running,
                RestartPolicy.Always,
                new DockerConfig(image),
                0,
                string.Empty,
                DateTime.MinValue,
                DateTime.MinValue,
                0,
                DateTime.MinValue,
                ModuleStatus.Running,
                ImagePullPolicy.OnCreate,
                new ConfigurationInfo("1"),
                new Dictionary<string, EnvVal>());

            // Act
            bool equal = edgeHubDockerModule.Equals(edgeHubDockerRuntimeModule);

            // Assert
            Assert.True(equal);
        }

        [Fact]
        [Unit]
        public void TestWithRuntimeStatus()
        {
            var module = new EdgeHubDockerRuntimeModule(
                ModuleStatus.Running,
                RestartPolicy.Always,
                new DockerConfig("edg0eHubImage"),
                0,
                string.Empty,
                DateTime.MinValue,
                DateTime.MinValue,
                0,
                DateTime.MinValue,
                ModuleStatus.Running,
                ImagePullPolicy.OnCreate,
                new ConfigurationInfo("1"),
                new Dictionary<string, EnvVal>());
            var updatedModule1 = (EdgeHubDockerRuntimeModule)module.WithRuntimeStatus(ModuleStatus.Running);
            var updatedModule2 = (EdgeHubDockerRuntimeModule)module.WithRuntimeStatus(ModuleStatus.Unknown);

            Assert.Equal(module, updatedModule1);
            Assert.NotEqual(module, updatedModule2);
            Assert.Equal(ModuleStatus.Unknown, updatedModule2.RuntimeStatus);
        }
    }
}
