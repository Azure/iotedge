// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.Test
{
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Newtonsoft.Json;
    using Xunit;

    public class AgentStateTest
    {
        public static IEnumerable<object[]> GetValidJsonInputs()
        {
            (object input, AgentState expected)[] inputs =
            {
                (new object(), new AgentState()),

                (new
                {
                    lastDesiredVersion = 10
                }, new AgentState(10)),

                (new
                {
                    lastDesiredVersion = 10,
                    lastDesiredStatus = new
                    {
                        code = 200,
                        description = string.Empty
                    }
                }, new AgentState(10, DeploymentStatus.Success)),

                (new
                {
                    lastDesiredStatus = new
                    {
                        code = 200,
                        description = string.Empty
                    }
                }, new AgentState(0, DeploymentStatus.Success)),

                (new
                {
                    schemaVersion = "2.0",
                    lastDesiredVersion = 10,
                    lastDesiredStatus = new
                    {
                        code = 200,
                        description = string.Empty
                    }
                }, new AgentState(10, DeploymentStatus.Success, schemaVersion: "2.0"))
            };

            return inputs.Select(r => new[] { r.input, r.expected });
        }

        [Theory]
        [Unit]
        [MemberData(nameof(GetValidJsonInputs))]
        public void DeserializeJsonTest(object input, AgentState expected)
        {
            // Arrange
            string json = JsonConvert.SerializeObject(input);

            // Act
            var state = JsonConvert.DeserializeObject<AgentState>(json);

            // Assert
            Assert.Equal(expected, state);
        }
    }
}
