// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    public class UpstreamProtocolTest
    {
        public static IEnumerable<object[]> UpstreamProtocolInputs()
        {
            yield return new object[] { null, Option.None<UpstreamProtocol>() };
            yield return new object[] { string.Empty, Option.None<UpstreamProtocol>() };
            yield return new object[] { "  ", Option.None<UpstreamProtocol>() };
            yield return new object[] { "Amqp", Option.Some(UpstreamProtocol.Amqp) };
            yield return new object[] { "AmqpWs", Option.Some(UpstreamProtocol.AmqpWs) };
            yield return new object[] { "Mqtt", Option.Some(UpstreamProtocol.Mqtt) };
            yield return new object[] { "MqttWs", Option.Some(UpstreamProtocol.MqttWs) };
            yield return new object[] { "AMQP", Option.Some(UpstreamProtocol.Amqp) };
            yield return new object[] { "AMQPWS", Option.Some(UpstreamProtocol.AmqpWs) };
            yield return new object[] { "Ampq", Option.None<UpstreamProtocol>() };
        }

        [Theory]
        [MemberData(nameof(UpstreamProtocolInputs))]
        [Unit]
        public void StringToUpstreamProtocolTest(string input, Option<UpstreamProtocol> expectedOutput)
        {
            // Act
            Option<UpstreamProtocol> receivedUpstreamProtocol = input.ToUpstreamProtocol();

            // Assert
            Assert.Equal(expectedOutput.HasValue, receivedUpstreamProtocol.HasValue);
            if (expectedOutput.HasValue)
            {
                Assert.Equal(expectedOutput.OrDefault(), receivedUpstreamProtocol.OrDefault());
            }
        }
    }
}
