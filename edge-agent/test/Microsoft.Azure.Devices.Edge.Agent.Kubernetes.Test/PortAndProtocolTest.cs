// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Test
{
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment.Service;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Unit]
    public class PortAndProtocolTest
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void UnableToParseEmptyString(string name)
        {
            var result = PortAndProtocol.Parse(name);

            Assert.Equal(Option.None<PortAndProtocol>(), result);
        }

        [Fact]
        public void UnableToParseUnsupportedProtocol()
        {
            var result = PortAndProtocol.Parse("123/HTTP");

            Assert.Equal(Option.None<PortAndProtocol>(), result);
        }

        [Fact]
        public void UnableToParseInvalidPort()
        {
            var result = PortAndProtocol.Parse("1a23/TCP");

            Assert.Equal(Option.None<PortAndProtocol>(), result);
        }

        [Fact]
        public void UnableToParseTooManyParts()
        {
            var result = PortAndProtocol.Parse("10/tcp/udp");

            Assert.Equal(Option.None<PortAndProtocol>(), result);
        }

        [Fact]
        public void DefaultProtocolIsTCP()
        {
            var result = PortAndProtocol.Parse("3434").OrDefault();

            Assert.Equal(3434, result.Port);
            Assert.Equal("TCP", result.Protocol);
        }

        [Theory]
        [InlineData("123/TCP", 123, "TCP")]
        [InlineData("456/UDP", 456, "UDP")]
        [InlineData("10/SCTP", 10, "SCTP")]
        public void ParsesPortAndProtocol(string name, int expectedPort, string expectedProtocol)
        {
            var result = PortAndProtocol.Parse(name).OrDefault();

            Assert.Equal(expectedPort, result.Port);
            Assert.Equal(expectedProtocol, result.Protocol);
        }
    }
}
