// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Test
{
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using global::Docker.DotNet.Models;
    using System;
    using System.Collections.Generic;
    using Xunit;

    [Unit]
    public class DockerUtilTest
    {
        [Fact]
        public void CannotParseHostnameFromNullImage()
        {
            string hostname;
            Assert.Throws<ArgumentNullException>(
                () => DockerUtil.TryParseHostnameFromImage(null, out hostname));
        }

        [Fact]
        public void CannotParseHostnameFromEmptyImage()
        {
            string hostname;
            Assert.False(DockerUtil.TryParseHostnameFromImage(string.Empty, out hostname));
            Assert.Empty(hostname);
        }

        [Fact]
        public void CannotParseMissingHostnameFromImage()
        {
            string hostname;
            Assert.False(DockerUtil.TryParseHostnameFromImage("nohostname", out hostname));
            Assert.Empty(hostname);
        }

        [Fact]
        public void CanParseHostnameFromImage()
        {
            string hostname;
            Assert.True(DockerUtil.TryParseHostnameFromImage("has/hostname/tricky", out hostname));
            Assert.Equal("has", hostname);
        }

        [Fact]
        public void ThrowsWhenImageArgumentIsNull()
        {
            var authConfigs = new List<AuthConfig>();
            Assert.Throws<ArgumentNullException>(
                () => DockerUtil.FirstAuthConfigOrDefault(null, authConfigs));
        }

        [Fact]
        public void ReturnsNullWhenAuthConfigListArgumentIsNull()
        {
            var authConfigs = new List<AuthConfig>();
            Assert.Null(DockerUtil.FirstAuthConfigOrDefault("dontcare", null));
        }

        [Fact]
        public void ReturnsNullWhenListIsEmpty()
        {
            var authConfigs = new List<AuthConfig>();
            AuthConfig found = DockerUtil.FirstAuthConfigOrDefault("hostname/imagename", authConfigs);
            Assert.Null(found);
        }

        [Fact]
        public void ReturnsTheFirstAuthConfigWhoseServerAddressMatchesTheImageHostname()
        {
            var authConfigs = new List<AuthConfig>
            {
                new AuthConfig { ServerAddress = "nope" },
                new AuthConfig { ServerAddress = "hostname" },
                new AuthConfig { ServerAddress = "nada" },
                new AuthConfig { ServerAddress = "hostname" }
            };

            AuthConfig found = DockerUtil.FirstAuthConfigOrDefault("hostname/imagename", authConfigs);
            Assert.Same(authConfigs[1], found);
        }
    }
}
