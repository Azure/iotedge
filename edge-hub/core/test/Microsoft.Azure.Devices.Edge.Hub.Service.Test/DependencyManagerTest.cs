// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service.Test
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Hub.CloudProxy;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Extensions.Configuration;
    using Xunit;

    [Unit]
    public class DependencyManagerTest
    {
        [Theory]
        [MemberData(nameof(GetUpstreamProtocolData))]
        public void ParseUpstreamProtocolTest(string input, Option<UpstreamProtocol> expectedValue)
        {
            // Arrange
            IConfigurationRoot configRoot = new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string>
                    {
                        { "UpstreamProtocol", input }
                    })
                .Build();

            // Act
            Option<UpstreamProtocol> upstreamProtocol = DependencyManager.GetUpstreamProtocol(configRoot);

            // Assert
            Assert.Equal(expectedValue.HasValue, upstreamProtocol.HasValue);
            Assert.Equal(expectedValue.OrDefault(), upstreamProtocol.OrDefault());
        }

        public static IEnumerable<object[]> GetUpstreamProtocolData()
        {
            yield return new object[] { "Mqtt", Option.Some(UpstreamProtocol.Mqtt) };
            yield return new object[] { "MQTT", Option.Some(UpstreamProtocol.Mqtt) };
            yield return new object[] { "mqtt", Option.Some(UpstreamProtocol.Mqtt) };
            yield return new object[] { "MqTt", Option.Some(UpstreamProtocol.Mqtt) };

            yield return new object[] { "Amqp", Option.Some(UpstreamProtocol.Amqp) };
            yield return new object[] { "AMQP", Option.Some(UpstreamProtocol.Amqp) };
            yield return new object[] { "amqp", Option.Some(UpstreamProtocol.Amqp) };
            yield return new object[] { "AmqP", Option.Some(UpstreamProtocol.Amqp) };

            yield return new object[] { "MqttWs", Option.Some(UpstreamProtocol.MqttWs) };
            yield return new object[] { "MQTTWS", Option.Some(UpstreamProtocol.MqttWs) };
            yield return new object[] { "mqttws", Option.Some(UpstreamProtocol.MqttWs) };
            yield return new object[] { "MqTtWs", Option.Some(UpstreamProtocol.MqttWs) };

            yield return new object[] { "AmqpWs", Option.Some(UpstreamProtocol.AmqpWs) };
            yield return new object[] { "AMQPWs", Option.Some(UpstreamProtocol.AmqpWs) };
            yield return new object[] { "amqpws", Option.Some(UpstreamProtocol.AmqpWs) };
            yield return new object[] { "amqPwS", Option.Some(UpstreamProtocol.AmqpWs) };

            yield return new object[] { "amqPwSt", Option.None<UpstreamProtocol>() };
            yield return new object[] { string.Empty, Option.None<UpstreamProtocol>() };
            yield return new object[] { "  ", Option.None<UpstreamProtocol>() };
            yield return new object[] { "mqttwebsockets", Option.None<UpstreamProtocol>() };
        }
    }
}
