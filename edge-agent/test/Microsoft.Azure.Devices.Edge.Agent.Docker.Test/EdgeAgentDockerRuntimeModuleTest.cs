// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Test
{
    using System;
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
            DateTime lastStartTimeUtc = DateTime.Parse(
                "2017-11-13T23:44:35.127381Z", null, DateTimeStyles.RoundtripKind
            );
            var module = new EdgeAgentDockerRuntimeModule(
                new DockerReportedConfig("booyah", string.Empty, "someSha"),
                ModuleStatus.Running,
                lastStartTimeUtc,
                new ConfigurationInfo("bing")
            );

            // Act
            JToken json = JToken.Parse(JsonConvert.SerializeObject(module));

            // Assert
            var expected = JToken.FromObject(new
            {
                runtimeStatus = "running",
                lastStartTimeUtc = lastStartTimeUtc,
                configuration = new
                {
                    id = "bing"
                },
                type = "docker",
                settings = new
                {
                    image = "booyah",
                    imageHash = "someSha",
                    createOptions = "{}"
                }
            });

            Assert.True(JToken.DeepEquals(expected, json));
        }

        [Fact]
        [Unit]
        public void TestJsonDeserialize()
        {
            // Arrange
            DateTime lastStartTimeUtc = DateTime.Parse(
                "2017-11-13T23:44:35.127381Z", null, DateTimeStyles.RoundtripKind
            );
            string json = JsonConvert.SerializeObject(new
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
            Assert.Equal("someImage", edgeAgent.Config.Image);
            Assert.Equal("someSha", (edgeAgent.Config as DockerReportedConfig).ImageHash);
            Assert.Equal(lastStartTimeUtc, edgeAgent.LastStartTimeUtc);
        }

        [Fact]
        [Unit]
        public void TestJsonDeserialize2()
        {
            // Arrange
            string json = JsonConvert.SerializeObject(new
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
            Assert.Equal("someImage", edgeAgent.Config.Image);
            Assert.Equal("someSha", (edgeAgent.Config as DockerReportedConfig).ImageHash);
            Assert.Equal("bing", edgeAgent.ConfigurationInfo.Id);
        }

        [Fact]
        [Unit]
        public void TestWithRuntimeStatus()
        {
            DateTime lastStartTimeUtc = DateTime.Parse(
              "2017-11-13T23:44:35.127381Z", null, DateTimeStyles.RoundtripKind
            );
            var module = new EdgeAgentDockerRuntimeModule(new DockerReportedConfig("booyah", string.Empty, "someSha"), ModuleStatus.Running, lastStartTimeUtc, new ConfigurationInfo("bing"));
            EdgeAgentDockerRuntimeModule updatedModule1 = (EdgeAgentDockerRuntimeModule)module.WithRuntimeStatus(ModuleStatus.Running);
            EdgeAgentDockerRuntimeModule updatedModule2 = (EdgeAgentDockerRuntimeModule)module.WithRuntimeStatus(ModuleStatus.Unknown);

            Assert.Equal(module, updatedModule1);
            //Assert.NotEqual(module, updatedModule2);
            Assert.Equal(updatedModule2.RuntimeStatus, ModuleStatus.Unknown);
        }
    }
}
