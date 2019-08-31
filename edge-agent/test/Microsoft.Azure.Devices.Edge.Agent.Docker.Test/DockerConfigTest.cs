// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Test
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Agent.Docker.Models;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Newtonsoft.Json;
    using Xunit;

    [ExcludeFromCodeCoverage]
    [Unit]
    public class DockerConfigTest
    {
        static readonly DockerConfig Config1 = new DockerConfig("image1:42");
        static readonly DockerConfig Config2 = new DockerConfig("image2:42");
        static readonly DockerConfig Config3 = new DockerConfig("image1:42");
        static readonly DockerConfig Config4 = new DockerConfig("image1:43");
        static readonly DockerConfig ConfigWithWhitespace = new DockerConfig(" image1:42 ");

        static readonly DockerConfig Config5 = new DockerConfig("image1:42", @"{""HostConfig"": {""PortBindings"": {""42/udp"": [{""HostPort"": ""42""}]}}}");
        static readonly DockerConfig Config6 = new DockerConfig("image1:42", @"{""Env"": [""k1=v1"", ""k2=v2""], ""HostConfig"": {""PortBindings"": {""43/udp"": [{""HostPort"": ""43""}]}}}");
        static readonly DockerConfig Config7 = new DockerConfig("image1:42", @"{""Env"": [""k1=v1"", ""k2=v2""], ""HostConfig"": {""PortBindings"": {""43/udp"": [{""HostPort"": ""43""}]}}}");
        static readonly DockerConfig Config8 = new DockerConfig("image1:42", @"{""Env"": [""k1=v1"", ""k2=v2"", ""k3=v3""], ""HostConfig"": {""PortBindings"": {""43/udp"": [{""HostPort"": ""43""}], ""42/tcp"": [{""HostPort"": ""42""}]}}}");
        static readonly DockerConfig Config9 = new DockerConfig("image1:42", @"{""Env"": [""k1=v1"", ""k2=v2"", ""k3=v3""], ""HostConfig"": {""PortBindings"": {""43/udp"": [{""HostPort"": ""43""}], ""42/tcp"": [{""HostPort"": ""42""}]}}}");

        static readonly DockerConfig Config10 = new DockerConfig("image1:42", @"{""Env"": [""k11=v11"", ""k22=v22""], ""HostConfig"": {""PortBindings"": {""43/udp"": [{""HostPort"": ""43""}]}}}");
        static readonly DockerConfig Config11 = new DockerConfig("image1:42", @"{""Env"": [""k33=v33"", ""k44=v44""], ""HostConfig"": {""PortBindings"": {""43/udp"": [{""HostPort"": ""43""}]}}}");
        static readonly DockerConfig Config12 = new DockerConfig("image1:42", string.Empty);
        static readonly DockerConfig Config13 = new DockerConfig("image1:42", "{}");
        static readonly DockerConfig Config14 = new DockerConfig("image1:42", "null");
        static readonly DockerConfig Config15 = new DockerConfig("image1:42", "  ");
        static readonly DockerConfig ConfigUnknown = new DockerConfig("unknown");
        static readonly DockerConfig ConfigUnknownExpected = new DockerConfig("unknown:latest");

        static readonly string Extended9 = @"
        {
            'image': 'image1:42',
            'createOptions': ""{'Env': ['k1=v1', 'k2=v2', 'k3=v3'], 'HostConfig'"",
            'createOptions01': "": {'PortBindings': {'43/udp': [{'HostPort': '4"",
            'createOptions02': ""3'}], '42/tcp': [{'HostPort': '42'}]}}}""
        }";

        static readonly string Extended9Order = @"
        {
            'image': 'image1:42',
            'createOptions01': "": {'PortBindings': {'43/udp': [{'HostPort': '4"",
            'createOptions02': ""3'}], '42/tcp': [{'HostPort': '42'}]}}}"",
            'createOptions': ""{'Env': ['k1=v1', 'k2=v2', 'k3=v3'], 'HostConfig'""
        }";

        static readonly string Extended9Error1 = @"
        {
            'image': 'image1:42',
            'createOptions': ""{'Env': ['k1=v1', 'k2=v2', 'k3=v3'], 'HostConfig'"",
            'createOptions02': "": {'PortBindings': {'43/udp': [{'HostPort': '4"",
            'createOptions03': ""3'}], '42/tcp': [{'HostPort': '42'}]}}}""
        }";

        static readonly string Extended9Error2 = @"
        {
            'image': 'image1:42',
            'createOptions': ""{'Env': ['k1=v1', 'k2=v2', 'k3=v3'], 'HostConfig'"",
            'createOptions02': ""3'}], '42/tcp': [{'HostPort': '42'}]}}}""
        }";

        static readonly string Extended9Error3 = @"
        {
            'image': 'image1:42',
            'createOptions00': ""{'Env': ['k1=v1', 'k2=v2', 'k3=v3'], 'HostConfig'"",
            'createOptions01': "": {'PortBindings': {'43/udp': [{'HostPort': '4"",
            'createOptions02': ""3'}], '42/tcp': [{'HostPort': '42'}]}}}""
        }";

        static readonly string Extended9Error4 = @"
        {
            'image': 'image1:42',
            'createOptions01': "": {'PortBindings': {'43/udp': [{'HostPort': '4"",
            'createOptions02': ""3'}], '42/tcp': [{'HostPort': '42'}]}}}""
        }";

        public static IEnumerable<object[]> GetTestGetCreateOptionsData()
        {
            yield return new object[] { null, new CreateContainerParameters() };

            yield return new object[] { " ", new CreateContainerParameters() };

            yield return new object[] { "null", new CreateContainerParameters() };

            string createOptions = @"{""HostConfig"": {""PortBindings"": {""42/udp"": [{""HostPort"": ""42""}]}}}";
            yield return new object[] { createOptions, JsonConvert.DeserializeObject<CreateContainerParameters>(createOptions) };
        }

        [Fact]
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
            Assert.Equal(Config1, ConfigWithWhitespace);
            Assert.Equal(Config1, Config12);
            Assert.Equal(Config1, Config13);
            Assert.Equal(Config1, Config14);
            Assert.Equal(Config1, Config15);
            Assert.Equal(ConfigUnknown, ConfigUnknownExpected);
        }

        [Fact]
        public void TestDeserialization()
        {
            Assert.Equal(Config9, JsonConvert.DeserializeObject<DockerConfig>(Extended9));
            Assert.Equal(Config9, JsonConvert.DeserializeObject<DockerConfig>(Extended9Order));
        }

        [Fact]
        public void TestDeserializationError()
        {
            var ex1 = Assert.Throws<JsonSerializationException>(() => JsonConvert.DeserializeObject<DockerConfig>(Extended9Error1));
            Assert.Equal("Error while parsing chunked field \"createOptions\", expected createOptions01 found createOptions02", ex1.Message);

            var ex2 = Assert.Throws<JsonSerializationException>(() => JsonConvert.DeserializeObject<DockerConfig>(Extended9Error2));
            Assert.Equal("Error while parsing chunked field \"createOptions\", expected createOptions01 found createOptions02", ex2.Message);

            var ex3 = Assert.Throws<JsonSerializationException>(() => JsonConvert.DeserializeObject<DockerConfig>(Extended9Error3));
            Assert.Equal("Error while parsing chunked field \"createOptions\", expected empty field number but found \"00\"", ex3.Message);

            var ex4 = Assert.Throws<JsonSerializationException>(() => JsonConvert.DeserializeObject<DockerConfig>(Extended9Error4));
            Assert.Equal("Error while parsing chunked field \"createOptions\", expected empty field number but found \"01\"", ex4.Message);
        }

        [Fact]
        public void TestSerialization()
        {
            var createOptions = @"{""Env"": [""k1=v1"", ""k2=v2"", ""k3=v3""], ""HostConfig"": {""PortBindings"": {""43/udp"": [" +
                                string.Join(", ", Enumerable.Repeat(@"{""HostPort"": ""43""}", 50)) +
                                @"], ""42/tcp"": [{""HostPort"": ""42""}]}}}";
            var config = new DockerConfig("image1:42", createOptions);
            var json = JsonConvert.SerializeObject(config);
            var expected = "{\"image\":\"image1:42\",\"createOptions\":\"{\\\"Env\\\":[\\\"k1=v1\\\",\\\"k2=v2\\\",\\\"k3=v3\\\"],\\\"HostConfig\\\":{\\\"PortBindings\\\":{\\\"43/udp\\\":[{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostP\",\"createOptions01\":\"ort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"},{\\\"HostPort\\\":\\\"43\\\"}],\\\"42/tcp\\\":[{\\\"HostPort\\\":\\\"42\\\"}]}}}\"}";
            Assert.Equal(expected, json);
        }

        [Theory]
        [InlineData(null, null, typeof(ArgumentException))]
        [InlineData("", null, typeof(ArgumentException))]
        [InlineData("  ", null, typeof(ArgumentException))]
        [InlineData("mcr.ms.com/ea:t1:t2", null, typeof(ArgumentException))]
        [InlineData("ea:t1:t2", null, typeof(ArgumentException))]
        [InlineData("ea:t1", "ea:t1", null)]
        [InlineData(" ea:t1 ", "ea:t1", null)]
        [InlineData("mcr.ms.com/ea:t1", "mcr.ms.com/ea:t1", null)]
        [InlineData("mcr.ms.com/ms/ea:t1", "mcr.ms.com/ms/ea:t1", null)]
        [InlineData("mcr.ms.com/ea", "mcr.ms.com/ea:latest", null)]
        [InlineData(" ubuntu ", "ubuntu:latest", null)]
        [InlineData("localhost:9000/ea", "localhost:9000/ea:latest", null)]
        [InlineData("localhost:9000/ea:tag1", "localhost:9000/ea:tag1", null)]
        [InlineData("localhost:9000/comp/ea:tag1", "localhost:9000/comp/ea:tag1", null)]
        public void TestValidateAndGetImage(string image, string result, Type expectedException)
        {
            if (expectedException != null)
            {
                Assert.Throws(expectedException, () => DockerConfig.ValidateAndGetImage(image));
            }
            else
            {
                string updatedImage = DockerConfig.ValidateAndGetImage(image);
                Assert.Equal(result, updatedImage);
            }
        }

        [Theory]
        [MemberData(nameof(GetTestGetCreateOptionsData))]
        public void TestGetCreateOptions(string createOptions, CreateContainerParameters expectedCreateOptions)
        {
            CreateContainerParameters createContainerParameters = DockerConfig.GetCreateOptions(createOptions);

            Assert.NotNull(createContainerParameters);
            Assert.True(DockerConfig.CompareCreateOptions(expectedCreateOptions, createContainerParameters));
        }
    }
}
