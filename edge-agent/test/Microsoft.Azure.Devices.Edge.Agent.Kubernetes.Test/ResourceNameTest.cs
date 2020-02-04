// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Test
{
    using System;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Unit]
    public class ResourceNameTest
    {
        [Theory]
        [InlineData(null, null)]
        [InlineData("host", null)]
        [InlineData(null, "device")]
        [InlineData("  ", "device")]
        [InlineData("host", "   ")]
        public void UnableToCreateInvalidResourceName(string hostname, string deviceId)
        {
            Assert.Throws<ArgumentException>(() => new ResourceName(hostname, deviceId));
        }

        [Fact]
        public void CreateValidResourceName()
        {
            var name = new ResourceName("hostname", "device");
            Assert.Equal("hostname", name.Hostname);
            Assert.Equal("device", name.DeviceId);
        }

        [Fact]
        public void RepresentsKubernetesResourceNameString()
        {
            var name = new ResourceName("hostname", "device");
            Assert.Equal("hostname-device", name.ToString());
        }

        [Fact]
        public void EqualsToKubernetesResourceNameString()
        {
            var name = new ResourceName("hostname", "device");
            Assert.Equal("hostname-device", name);
        }
    }
}
