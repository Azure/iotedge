// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Test
{
    using System.Diagnostics.CodeAnalysis;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [ExcludeFromCodeCoverage]
    public class DockerReportedConfigTest
    {
        static readonly DockerReportedConfig Config1 = new DockerReportedConfig("image1:42", @"{""HostConfig"": {""PortBindings"": {""42/udp"": [{""HostPort"": ""42""}]}}}", "sha256:foo");
        static readonly DockerReportedConfig Config2 = new DockerReportedConfig("image1:42", @"{""Env"": [""k1=v1"", ""k2=v2""], ""HostConfig"": {""PortBindings"": {""43/udp"": [{""HostPort"": ""43""}]}}}", "sha256:foo");
        static readonly DockerReportedConfig Config3 = new DockerReportedConfig("image1:42", @"{""Env"": [""k1=v1"", ""k2=v2""], ""HostConfig"": {""PortBindings"": {""43/udp"": [{""HostPort"": ""43""}]}}}", "sha256:foo");
        static readonly DockerReportedConfig Config4 = new DockerReportedConfig("image1:43", @"{""Env"": [""k1=v1"", ""k2=v2""], ""HostConfig"": {""PortBindings"": {""43/udp"": [{""HostPort"": ""43""}]}}}", "sha256:foo");
        static readonly DockerReportedConfig Config5 = new DockerReportedConfig("image1:43", @"{""Env"": [""k1=v1"", ""k2=v2""], ""HostConfig"": {""PortBindings"": {""43/udp"": [{""HostPort"": ""43""}]}}}", "sha256:foobar");

        static readonly DockerReportedConfig Config10 = new DockerReportedConfig("image1:42", @"{""Env"": [""k11=v11"", ""k22=v22""], ""HostConfig"": {""PortBindings"": {""43/udp"": [{""HostPort"": ""43""}]}}}", "sha256:foo");
        static readonly DockerReportedConfig Config11 = new DockerReportedConfig("image1:42", @"{""Env"": [""k33=v33"", ""k44=v44""], ""HostConfig"": {""PortBindings"": {""43/udp"": [{""HostPort"": ""43""}]}}}", "sha256:foo");

        [Fact]
        [Unit]
        public void TestEquality()
        {
            Assert.NotEqual(Config1, Config2);
            Assert.Equal(Config2, Config3);
            Assert.NotEqual(Config3, Config4);
            Assert.NotEqual(Config4, Config5);
        }
    }
}
