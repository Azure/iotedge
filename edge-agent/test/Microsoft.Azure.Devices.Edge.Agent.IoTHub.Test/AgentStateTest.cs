// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.Test
{
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Newtonsoft.Json;
    using System.Collections.Generic;
    using System.Linq;
    using Xunit;

    public class AgentStateTest
    {
        static IEnumerable<object[]> GetValidJsonInputs()
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
                        description = ""
                    }
                }, new AgentState(10, DeploymentStatus.Success)),

                (new
                {
                    lastDesiredStatus = new
                    {
                        code = 200,
                        description = ""
                    }
                }, new AgentState(0, DeploymentStatus.Success)),
            };

            return inputs.Select(r => new object[] { r.input, r.expected });
        }

        [Theory]
        [Unit]
        [MemberData(nameof(GetValidJsonInputs))]
        public void DeserializeJsonTest(object input, AgentState expected)
        {
            // Arrange
            string json = JsonConvert.SerializeObject(input);

            // Act
            AgentState state = JsonConvert.DeserializeObject<AgentState>(json);

            // Assert
            Assert.Equal(expected, state);
        }
    }
}
