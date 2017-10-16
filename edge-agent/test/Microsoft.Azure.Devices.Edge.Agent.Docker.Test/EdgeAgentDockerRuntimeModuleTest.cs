// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Test
{
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
            var module = new EdgeAgentDockerRuntimeModule(new DockerConfig("booyah"), ModuleStatus.Running, new ConfigurationInfo("bing"));

            // Act
            JToken json = JToken.Parse(JsonConvert.SerializeObject(module));

            // Assert
            var expected = JToken.FromObject(new
            {
                runtimeStatus = "running",
                settings = new
                {
                    image = "booyah",
                    createOptions = "{}"
                },
                configuration = new
                {
                    id = "bing"
                }
            });
            Assert.True(JToken.DeepEquals(expected, json));
        }
    }
}
