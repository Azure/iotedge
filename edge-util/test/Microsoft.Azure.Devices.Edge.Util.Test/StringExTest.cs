// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util.Test
{
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Unit]
    public class StringExTest
    {
        [Fact]
        public void EvenChunksTest()
        {
            var str = "aaabbb";
            var expected = new List<string> { "aaa", "bbb" };
            Assert.Equal(expected, str.Chunks(3).ToList());
        }

        [Fact]
        public void UnevenChunksTest()
        {
            var str = "aaabb";
            var expected = new List<string> { "aaa", "bb" };
            Assert.Equal(expected, str.Chunks(3).ToList());
        }
    }
}
