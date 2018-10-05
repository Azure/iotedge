// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Test
{
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Newtonsoft.Json;
    using Xunit;

    [ExcludeFromCodeCoverage]
    [Unit]
    public class DockerReportedConfigTest
    {
        static readonly DockerReportedConfig Config1 = new DockerReportedConfig("image1:42", @"{""HostConfig"": {""PortBindings"": {""42/udp"": [{""HostPort"": ""42""}]}}}", "sha256:foo");
        static readonly DockerReportedConfig Config2 = new DockerReportedConfig("image1:42", @"{""Env"": [""k1=v1"", ""k2=v2""], ""HostConfig"": {""PortBindings"": {""43/udp"": [{""HostPort"": ""43""}]}}}", "sha256:foo");
        static readonly DockerReportedConfig Config3 = new DockerReportedConfig("image1:42", @"{""Env"": [""k1=v1"", ""k2=v2""], ""HostConfig"": {""PortBindings"": {""43/udp"": [{""HostPort"": ""43""}]}}}", "sha256:foo");
        static readonly DockerReportedConfig Config4 = new DockerReportedConfig("image1:43", @"{""Env"": [""k1=v1"", ""k2=v2""], ""HostConfig"": {""PortBindings"": {""43/udp"": [{""HostPort"": ""43""}]}}}", "sha256:foo");
        static readonly DockerReportedConfig Config5 = new DockerReportedConfig("image1:43", @"{""Env"": [""k1=v1"", ""k2=v2""], ""HostConfig"": {""PortBindings"": {""43/udp"": [{""HostPort"": ""43""}]}}}", "sha256:foosha");

        static readonly string Extended5 = @"
        {
            'image': 'image1:43',
            'imageHash': 'sha256:foosha',
            'createOptions': ""{'Env': ['k1=v1', 'k2=v2'], 'HostConfig'"",
            'createOptions01': "": {'PortBindings': {'43/udp': [{'HostPort': '4"",
            'createOptions02': ""3'}]}}}""
        }";

        static readonly string Extended5Order = @"
        {
            'image': 'image1:43',
            'imageHash': 'sha256:foosha',
            'createOptions01': "": {'PortBindings': {'43/udp': [{'HostPort': '4"",
            'createOptions02': ""3'}]}}}"",
            'createOptions': ""{'Env': ['k1=v1', 'k2=v2'], 'HostConfig'""
        }";

        static readonly string Extended5Error1 = @"
        {
            'image': 'image1:43',
            'imageHash': 'sha256:foosha',
            'createOptions': ""{'Env': ['k1=v1', 'k2=v2'], 'HostConfig'"",
            'createOptions02': "": {'PortBindings': {'43/udp': [{'HostPort': '4"",
            'createOptions03': ""3'}]}}}""
        }";

        static readonly string Extended5Error2 = @"
        {
            'image': 'image1:43',
            'imageHash': 'sha256:foosha',
            'createOptions': ""{'Env': ['k1=v1', 'k2=v2'], 'HostConfig'"",
            'createOptions02': ""3'}]}}}""
        }";

        static readonly string Extended5Error3 = @"
        {
            'image': 'image1:43',
            'imageHash': 'sha256:foosha',
            'createOptions00': ""{'Env': ['k1=v1', 'k2=v2'], 'HostConfig'"",
            'createOptions01': "": {'PortBindings': {'43/udp': [{'HostPort': '4"",
            'createOptions02': ""3'}]}}}""
        }";

        static readonly string Extended5Error4 = @"
        {
            'image': 'image1:43',
            'imageHash': 'sha256:foosha',
            'createOptions01': "": {'PortBindings': {'43/udp': [{'HostPort': '4"",
            'createOptions02': ""3'}]}}}""
        }";

        [Fact]
        public void TestEquality()
        {
            Assert.NotEqual(Config1, Config2);
            Assert.Equal(Config2, Config3);
            Assert.NotEqual(Config3, Config4);
            Assert.NotEqual(Config4, Config5);
        }

        [Fact]
        public void TestDeserialization()
        {
            Assert.Equal(Config5, JsonConvert.DeserializeObject<DockerReportedConfig>(Extended5));
            Assert.Equal(Config5, JsonConvert.DeserializeObject<DockerReportedConfig>(Extended5Order));
        }

        [Fact]
        public void TestDeserializationError()
        {
            var ex1 = Assert.Throws<JsonSerializationException>(() => JsonConvert.DeserializeObject<DockerReportedConfig>(Extended5Error1));
            Assert.Equal("Error while parsing chunked field \"createOptions\", expected createOptions01 found createOptions02", ex1.Message);

            var ex2 = Assert.Throws<JsonSerializationException>(() => JsonConvert.DeserializeObject<DockerReportedConfig>(Extended5Error2));
            Assert.Equal("Error while parsing chunked field \"createOptions\", expected createOptions01 found createOptions02", ex2.Message);

            var ex3 = Assert.Throws<JsonSerializationException>(() => JsonConvert.DeserializeObject<DockerReportedConfig>(Extended5Error3));
            Assert.Equal("Error while parsing chunked field \"createOptions\", expected empty field number but found \"00\"", ex3.Message);

            var ex4 = Assert.Throws<JsonSerializationException>(() => JsonConvert.DeserializeObject<DockerReportedConfig>(Extended5Error4));
            Assert.Equal("Error while parsing chunked field \"createOptions\", expected empty field number but found \"01\"", ex4.Message);
        }

        [Fact]
        public void TestSerialization()
        {
            var createOptions = @"{""Env"": [""k1=v1"", ""k2=v2"", ""k3=v3""], ""HostConfig"": {""PortBindings"": {""43/udp"": [" +
                string.Join(", ", Enumerable.Repeat(@"{""HostPort"": ""43""}", 50)) +
                @"], ""42/tcp"": [{""HostPort"": ""42""}]}}}";
            var config = new DockerReportedConfig("image1:42", createOptions, "sha256:foosha");
            var json = JsonConvert.SerializeObject(config);
            var expected = "{\"image\":\"image1:42\",\"imageHash\":\"sha256:foosha\",\"createOptions\":\"{\\\"Env\\\":[\\\"k1=v1\\\",\\\"k2=v2\\\",\\\"k3=v3\\\"],\\\"HostConfig\\\":{\\\"PortBindings\\\":{\\\"43/udp\\\":[{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostP\",\"createOptions01\":\"ort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"}],\\\"42/tcp\\\":[{\\\"HostPort\\\":\\\"42\\\"}]}}}\"}";
            Assert.Equal(expected, json);
        }
    }
}
