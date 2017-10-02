// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Test
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [ExcludeFromCodeCoverage]
    public class DockerConfigTest
    {
        static readonly DockerConfig Config1 = new DockerConfig("image1", "42");
        static readonly DockerConfig Config2 = new DockerConfig("image2", "42");
        static readonly DockerConfig Config3 = new DockerConfig("image1", "42");
        static readonly DockerConfig Config4 = new DockerConfig("image1", "43");
        
        static readonly DockerConfig Config5 = new DockerConfig("image1", "42", @"{""HostConfig"": {""PortBindings"": {""42/udp"": [{""HostPort"": ""42""}]}}}");
        static readonly DockerConfig Config6 = new DockerConfig("image1", "42", @"{""Env"": [""k1=v1"", ""k2=v2""], ""HostConfig"": {""PortBindings"": {""43/udp"": [{""HostPort"": ""43""}]}}}");
        static readonly DockerConfig Config7 = new DockerConfig("image1", "42", @"{""Env"": [""k1=v1"", ""k2=v2""], ""HostConfig"": {""PortBindings"": {""43/udp"": [{""HostPort"": ""43""}]}}}");
        static readonly DockerConfig Config8 = new DockerConfig("image1", "42", @"{""Env"": [""k1=v1"", ""k2=v2"", ""k3=v3""], ""HostConfig"": {""PortBindings"": {""43/udp"": [{""HostPort"": ""43""}], ""42/tcp"": [{""HostPort"": ""42""}]}}}");
        static readonly DockerConfig Config9 = new DockerConfig("image1", "42", @"{""Env"": [""k1=v1"", ""k2=v2"", ""k3=v3""], ""HostConfig"": {""PortBindings"": {""43/udp"": [{""HostPort"": ""43""}], ""42/tcp"": [{""HostPort"": ""42""}]}}}");

        static readonly DockerConfig Config10 = new DockerConfig("image1", "42", @"{""Env"": [""k11=v11"", ""k22=v22""], ""HostConfig"": {""PortBindings"": {""43/udp"": [{""HostPort"": ""43""}]}}}");
        static readonly DockerConfig Config11 = new DockerConfig("image1", "42", @"{""Env"": [""k33=v33"", ""k44=v44""], ""HostConfig"": {""PortBindings"": {""43/udp"": [{""HostPort"": ""43""}]}}}");

        [Fact]
        [Unit]
        public void TestConstructor()
        {
            Assert.Throws<ArgumentException>(() => new DockerConfig(null, "42"));
            Assert.Throws<ArgumentException>(() => new DockerConfig("image1", null));
        }

        [Fact]
        [Unit]
        public void TestEquality()
        {
            Assert.Equal(Config1, Config1);
            Assert.Equal(Config1, Config3);
            Assert.Equal(Config6, Config7);
            Assert.Equal(Config8, Config9);
            Assert.NotEqual(Config1, Config2);
            Assert.NotEqual(Config3, Config4);
            Assert.NotEqual(Config5, Config6);
            Assert.NotEqual(Config10, Config11);
            Assert.True(Config1.Equals((object)Config1));
            Assert.False(Config1.Equals(null));
            Assert.False(Config1.Equals((object)null));
            Assert.False(Config1.Equals(new object()));
        }
    }
}