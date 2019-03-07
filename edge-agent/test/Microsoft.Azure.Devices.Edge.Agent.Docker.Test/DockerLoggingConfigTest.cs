// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Test
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    public class DockerLoggingConfigTest
    {
        static readonly Dictionary<string, string> LoggingConfig1 = new Dictionary<string, string> { { "k1", "v1" }, { "k2", "v2" } };
        static readonly DockerLoggingConfig Config1 = new DockerLoggingConfig("json-file");
        static readonly DockerLoggingConfig Config2 = new DockerLoggingConfig("journald");
        static readonly DockerLoggingConfig Config3 = new DockerLoggingConfig("json-file", new Dictionary<string, string> { { "k1", "v1" }, { "k2", "v2" } });
        static readonly DockerLoggingConfig Config4 = new DockerLoggingConfig("json-file", new Dictionary<string, string> { { "k1", "v1" }, { "k3", "v3" } });
        static readonly DockerLoggingConfig Config5 = new DockerLoggingConfig("json-file", new Dictionary<string, string> { { "k1", "v1" }, { "k2", "v2" } });
        static readonly DockerLoggingConfig Config6 = new DockerLoggingConfig("json-file", LoggingConfig1);
        static readonly DockerLoggingConfig Config7 = new DockerLoggingConfig("json-file", LoggingConfig1);
        static readonly DockerLoggingConfig Config8 = new DockerLoggingConfig("json-file", new Dictionary<string, string> { { "k1", "v1" } });
        static readonly DockerLoggingConfig Config9 = new DockerLoggingConfig("json-file", new Dictionary<string, string> { { "k1", "v2" } });

        [Fact]
        [Unit]
        public void TestConstruction()
        {
            Assert.Throws<ArgumentException>(() => new DockerLoggingConfig(null, ImmutableDictionary<string, string>.Empty));
            Assert.Throws<ArgumentNullException>(() => new DockerLoggingConfig("not null", null));
            Assert.Throws<ArgumentException>(() => new DockerLoggingConfig(" "));
        }

        [Fact]
        [Unit]
        public void TestEquals()
        {
            Assert.False(Config1.Equals(null));
            Assert.True(Config1.Equals((object)Config1));
            Assert.False(Config1.Equals(Config3));
            Assert.True(Config3.Equals(Config5));
            Assert.False(Config4.Equals(Config3));
            Assert.True(Config6.Equals(Config7));
            Assert.False(Config6.Equals(Config8));
            Assert.False(Config8.Equals(Config9));
        }

        [Fact]
        [Unit]
        public void TestHashCode()
        {
            int hash1 = Config1.GetHashCode();
            int hash2 = Config2.GetHashCode();
            int hash3 = Config3.GetHashCode();
            int hash4 = Config4.GetHashCode();
            int hash5 = Config5.GetHashCode();

            Assert.False(hash1 == hash2);
            Assert.False(hash1 == hash3);
            Assert.False(hash3 == hash4);
            Assert.True(hash3 == hash5);
        }
    }
}
