// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test
{
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
    }
}
