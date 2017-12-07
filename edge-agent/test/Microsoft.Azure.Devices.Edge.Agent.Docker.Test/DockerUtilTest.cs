// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Test
{
    using System;
    using System.Collections.Generic;
    using global::Docker.DotNet.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Unit]
    public class DockerUtilTest
    {
        [Fact]
        public void CannotParseHostnameFromNullImage()
        {
            Assert.Throws<ArgumentNullException>(() => DockerUtil.TryParseDomainFromImage(null, out string _));
        }

        [Fact]
        public void CannotParseHostnameFromEmptyImage()
        {
            Assert.False(DockerUtil.TryParseDomainFromImage(string.Empty, out string hostname));
            Assert.Empty(hostname);
        }

        [Fact]
        public void MissingHostnameReturnsDefault()
        {
            Assert.True(DockerUtil.TryParseDomainFromImage("repo/image", out string hostname));
            Assert.Equal(Constants.DefaultRegistryAddress, hostname);
        }

        [Theory]
        [InlineData("miyagley/edge-hub", Constants.DefaultRegistryAddress)]
        [InlineData("edgepreview.azurecr.io/image", "edgepreview.azurecr.io")]
        [InlineData("edgepreview.azurecr.io/namespace/image", "edgepreview.azurecr.io")]
        [InlineData("edgepreview.azurecr.io/namespace/image/another", "edgepreview.azurecr.io")]
        [InlineData("localhost:5000/image", "localhost:5000")]
        [InlineData("localhost:5000/namespace/image", "localhost:5000")]
        public void CanParseHostnameFromImage(string image, string expectedHostname)
        {
            Assert.True(DockerUtil.TryParseDomainFromImage(image, out string hostname));
            Assert.Equal(expectedHostname, hostname);
        }

        [Fact]
        public void ThrowsWhenImageArgumentIsNull()
        {
            var authConfigs = new List<AuthConfig>();
            Assert.Throws<ArgumentNullException>(() => authConfigs.FirstAuthConfig(null));
        }

        [Fact]
        public void ReturnsNullWhenAuthConfigListArgumentIsNull()
        {
            Assert.Equal(Option.None<AuthConfig>(), ((IEnumerable<AuthConfig>)null).FirstAuthConfig("dontcare"));
        }

        [Fact]
        public void ReturnsNullWhenListIsEmpty()
        {
            var authConfigs = new List<AuthConfig>();
            Option<AuthConfig> found = authConfigs.FirstAuthConfig("hostname/repo/imagename");
            Assert.False(found.HasValue);
        }

        [Fact]
        public void ReturnsTheFirstAuthConfigWhoseServerAddressMatchesTheImageHostname()
        {
            var authConfigs = new List<AuthConfig>
            {
                new AuthConfig { ServerAddress = "nope" },
                new AuthConfig { ServerAddress = Constants.DefaultRegistryAddress },
                new AuthConfig { ServerAddress = "nada" },
                new AuthConfig { ServerAddress = "hostname" }
            };

            Option<AuthConfig> found = authConfigs.FirstAuthConfig("hostname/imagename");
            Assert.Same(authConfigs[1], found.OrDefault());
        }
    }
}
