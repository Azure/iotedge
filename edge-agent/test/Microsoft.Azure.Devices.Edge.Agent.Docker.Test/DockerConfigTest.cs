// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Test
{
    using System;
    using System.Collections.Generic;
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
        
        static readonly DockerConfig Config5 = new DockerConfig("image1", "42", new HashSet<PortBinding> { new PortBinding("42", "42", PortBindingType.Udp) });
        static readonly DockerConfig Config6 = new DockerConfig("image1", "42", new HashSet<PortBinding> { new PortBinding("43", "43", PortBindingType.Udp) });
        static readonly DockerConfig Config7 = new DockerConfig("image1", "42", new HashSet<PortBinding> { new PortBinding("43", "43", PortBindingType.Udp) });
        static readonly DockerConfig Config8 = new DockerConfig("image1", "42", new HashSet<PortBinding> { new PortBinding("43", "43", PortBindingType.Udp), new PortBinding("42", "42", PortBindingType.Tcp) });
        static readonly DockerConfig Config9 = new DockerConfig("image1", "42", new HashSet<PortBinding> { new PortBinding("42", "42", PortBindingType.Tcp), new PortBinding("43", "43", PortBindingType.Udp) });

        [Fact]
        [Unit]
        public void TestConstructor()
        {
            Assert.Throws<ArgumentNullException>(() => new DockerConfig(null, "42"));
            Assert.Throws<ArgumentNullException>(() => new DockerConfig("image1", null));
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
            Assert.True(Config1.Equals((object)Config1));
            Assert.False(Config1.Equals(null));
            Assert.False(Config1.Equals((object)null));
            Assert.False(Config1.Equals(new object()));
        }
    }
}