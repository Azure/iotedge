// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Test
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Xunit;

    public class EdgeAgentDockerRuntimeModuleTest
    {
        [Fact]
        [Unit]
        public void TestJsonSerialize()
        {
            // Arrange
            DateTime lastStartTimeUtc = DateTime.Parse("2017-11-13T23:44:35.127381Z", null, DateTimeStyles.RoundtripKind);
            DateTime lastExitTimeUtc = DateTime.Parse("2017-11-13T23:49:35.127381Z", null, DateTimeStyles.RoundtripKind);
            var module = new EdgeAgentDockerRuntimeModule(
                new DockerReportedConfig("booyah:latest", string.Empty, "someSha"),
                ModuleStatus.Running,
                0,
                string.Empty,
                lastStartTimeUtc,
                lastExitTimeUtc,
                ImagePullPolicy.OnCreate,
                null,
                new Dictionary<string, EnvVal>());

            // Act
            JToken json = JToken.Parse(JsonConvert.SerializeObject(module));

            // Assert
            JToken expected = JToken.FromObject(
                new
                {
                    runtimeStatus = "running",
                    exitCode = 0,
                    lastStartTimeUtc = lastStartTimeUtc,
                    lastExitTimeUtc = lastExitTimeUtc,
                    statusDescription = string.Empty,
                    type = "docker",
                    imagePullPolicy = "on-create",
                    settings = new
                    {
                        image = "booyah:latest",
                        imageHash = "someSha",
                        createOptions = "{}"
                    },
                    env = new { }
                });

            Assert.True(JToken.DeepEquals(expected, json));
        }

        [Fact]
        [Unit]
        public void TestJsonDeserialize()
        {
            // Arrange
            DateTime lastStartTimeUtc = DateTime.Parse(
                "2017-11-13T23:44:35.127381Z",
                null,
                DateTimeStyles.RoundtripKind);
            string json = JsonConvert.SerializeObject(
                new
                {
                    type = "docker",
                    runtimeStatus = "running",
                    settings = new
                    {
                        image = "someImage",
                        createOptions = "{}",
                        imageHash = "someSha"
                    },
                    lastStartTimeUtc = lastStartTimeUtc
                });

            // Act
            var edgeAgent = JsonConvert.DeserializeObject<EdgeAgentDockerRuntimeModule>(json);

            // Assert
            Assert.Equal("docker", edgeAgent.Type);
            Assert.Equal(ModuleStatus.Running, edgeAgent.RuntimeStatus);
            Assert.Equal("someImage:latest", edgeAgent.Config.Image);
            // TODO - Change Config for Runtime to DockerReportedConfig.
            // Assert.Equal("someSha", (edgeAgent.Config as DockerReportedConfig)?.ImageHash);
            Assert.Equal(lastStartTimeUtc, edgeAgent.LastStartTimeUtc);
        }

        [Fact]
        [Unit]
        public void TestJsonDeserialize2()
        {
            // Arrange
            string json = JsonConvert.SerializeObject(
                new
                {
                    type = "docker",
                    runtimeStatus = "running",
                    settings = new
                    {
                        image = "someImage",
                        createOptions = "{}",
                        imageHash = "someSha"
                    },
                    configuration = new
                    {
                        id = "bing"
                    }
                });

            // Act
            var edgeAgent = JsonConvert.DeserializeObject<EdgeAgentDockerRuntimeModule>(json);

            // Assert
            Assert.Equal("docker", edgeAgent.Type);
            Assert.Equal(ModuleStatus.Running, edgeAgent.RuntimeStatus);
            Assert.Equal("someImage:latest", edgeAgent.Config.Image);
            // TODO - Change Config for Runtime to DockerReportedConfig.
            // Assert.Equal("someSha", (edgeAgent.Config as DockerReportedConfig)?.ImageHash);
            Assert.Equal("bing", edgeAgent.ConfigurationInfo.Id);
        }

        [Fact]
        [Unit]
        public void TestWithRuntimeStatus()
        {
            DateTime lastStartTimeUtc = DateTime.Parse("2017-11-13T23:44:35.127381Z", null, DateTimeStyles.RoundtripKind);
            DateTime lastExitTimeUtc = DateTime.Parse("2017-11-13T23:49:35.127381Z", null, DateTimeStyles.RoundtripKind);
            var module = new EdgeAgentDockerRuntimeModule(
                new DockerReportedConfig("booyah", string.Empty, "someSha"),
                ModuleStatus.Running,
                0,
                string.Empty,
                lastStartTimeUtc,
                lastExitTimeUtc,
                ImagePullPolicy.OnCreate,
                new ConfigurationInfo("bing"),
                new Dictionary<string, EnvVal>());
            var updatedModule1 = (EdgeAgentDockerRuntimeModule)module.WithRuntimeStatus(ModuleStatus.Running);
            var updatedModule2 = (EdgeAgentDockerRuntimeModule)module.WithRuntimeStatus(ModuleStatus.Unknown);

            Assert.Equal(module, updatedModule1);
            Assert.NotEqual(module, updatedModule2);
            Assert.Equal(ModuleStatus.Unknown, updatedModule2.RuntimeStatus);
        }
    }
}
