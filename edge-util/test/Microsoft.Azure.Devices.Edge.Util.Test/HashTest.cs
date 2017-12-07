// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util.Test
{
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Unit]
    public class HashTest
    {
        [Fact]
        public void BasicTestSha256()
        {
            const string Expected = "WoMr0G2wDlnPOwUevA6j5m6TJpBwE3FgkRJVtfzZeb8=";
            Assert.Equal(Expected, Hash.CreateSha256("say what"));
        }
    }
}
