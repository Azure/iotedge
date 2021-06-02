// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test
{
    using System;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Unit]
    public class AuthChainHelpersTest
    {
        [Fact]
        public void ValidateChainTest()
        {
            // Correct case
            Assert.True(AuthChainHelpers.ValidateAuthChain("edge1", "leaf1", "leaf1;edge1;edgeRoot"));

            // Unauthorized actor
            Assert.False(AuthChainHelpers.ValidateAuthChain("edge1", "leaf1", "leaf1;edge2;edgeRoot"));

            // Bad target
            Assert.False(AuthChainHelpers.ValidateAuthChain("edge1", "leaf1", "leaf2;edge1;edgeRoot"));

            // Invalid format
            Assert.False(AuthChainHelpers.ValidateAuthChain("edge1", "leaf1", ";"));
        }

        [Fact]
        public void GetAuthChainIds_Success()
        {
            Assert.Equal(new[] { "device1/$edgeHub", "device1", "device2" }, AuthChainHelpers.GetAuthChainIds("device1/$edgeHub;device1;device2"));

            Assert.Equal(new[] { "longdevicename" }, AuthChainHelpers.GetAuthChainIds("longdevicename"));
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData(null)]
        public void GetAuthChainIds_Fail(string authChain)
        {
            Assert.Throws<ArgumentException>(() => AuthChainHelpers.GetAuthChainIds(authChain));
        }

        [Theory]
        [InlineData("device1;device2;device3", true, "device1")]
        [InlineData("device1;device2", true, "device1")]
        [InlineData("longdevicename", true, "longdevicename")]
        [InlineData("device1/$edgeHub;device1;device2", false, null)]
        [InlineData("   ", false, null)]
        [InlineData("", false, null)]
        [InlineData(null, false, null)]
        public void TryGetTargetDeviceId_Success(string authChain, bool expected, string expectedTargetDeviceId)
        {
            bool actual = AuthChainHelpers.TryGetTargetDeviceId(authChain, out string actualTargetDeviceId);
            Assert.Equal(expected, actual);
            if (actual)
            {
                Assert.Equal(expectedTargetDeviceId, actualTargetDeviceId);
            }
        }
    }
}
