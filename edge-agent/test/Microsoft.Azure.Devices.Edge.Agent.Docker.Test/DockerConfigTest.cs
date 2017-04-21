// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Test
{
    using System;
    using Xunit;

    public class DockerConfigTest
    {

        static readonly DockerConfig Config1 = new DockerConfig("image1");
        static readonly DockerConfig Config2 = new DockerConfig("image2");
        static readonly DockerConfig Config3 = new DockerConfig("image1");

        [Fact]
        public void TestConstructor()
        {
            Assert.Throws<ArgumentNullException>(() => new DockerConfig(null));
        }

        [Fact]
        public void TestEquality()
        {
            Assert.Equal(Config1, Config1);
            Assert.Equal(Config1, Config3);
            Assert.NotEqual(Config1, Config2);
            Assert.True(Config1.Equals((object)Config1));
            Assert.False(Config1.Equals(null));
            Assert.False(Config1.Equals((object)null));
            Assert.False(Config1.Equals(new object()));
        }
    }
}