// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Test
{
    using System;
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
                Core.Constants.EdgeHubModuleName, string.Empty,
                ModuleStatus.Running, RestartPolicy.Always,
                new DockerConfig("edg0eHubImage"), 0, string.Empty,
                DateTime.MinValue, DateTime.MinValue, 0,
                DateTime.MinValue, ModuleStatus.Running,
                new ConfigurationInfo("1")
            );

            // Act
            JToken json = JToken.Parse(JsonConvert.SerializeObject(module));

            // Assert
            var expected = JToken.Parse(@"
{
  ""status"": ""running"",
  ""restartPolicy"": ""always"",
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
  },
  ""configuration"": {
    ""id"": ""1""
  }
}
            ");

            Assert.True(JToken.DeepEquals(expected, json));
        }

        [Fact]
        [Unit]
        public void TestJsonDeerialize()
        {
            // Arrange
            var json = @"
{
  ""status"": ""running"",
  ""restartPolicy"": ""always"",
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
  },
  ""configuration"": {
    ""id"": ""1""
  }
}";

            // Act
            EdgeHubDockerRuntimeModule actual = JsonConvert.DeserializeObject<EdgeHubDockerRuntimeModule>(json);

            // Assert
            var expected = new EdgeHubDockerRuntimeModule(
                Core.Constants.EdgeHubModuleName, string.Empty,
                ModuleStatus.Running, RestartPolicy.Always,
                new DockerConfig("edg0eHubImage"), 0, string.Empty,
                DateTime.MinValue, DateTime.MinValue, 0,
                DateTime.MinValue, ModuleStatus.Running,
                new ConfigurationInfo("1")
            );

            Assert.Equal(expected, actual);
        }
    }
}
