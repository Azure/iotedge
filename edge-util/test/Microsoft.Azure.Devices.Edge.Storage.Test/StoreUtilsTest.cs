// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage.Test
{
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Unit]
    public class StoreUtilsTest
    {
        [Fact]
        public void KeyToOffsetConversionTest()
        {
            for (long offset = 0; offset < 100000; offset++)
            {
                byte[] key = StoreUtils.GetKeyFromOffset(offset);
                long offset2 = StoreUtils.GetOffsetFromKey(key);
                Assert.Equal(offset, offset2);
            }
        }

        [Fact]
        public void TestOperation()
        {
            long offset = 1000;
            byte[] key = StoreUtils.GetKeyFromOffset(offset);
            byte[] obtainedKey = key.ToArray();
            Assert.Equal(key, obtainedKey);

            long obtainedOffset = StoreUtils.GetOffsetFromKey(key);
            Assert.Equal(offset, obtainedOffset);
            Assert.Equal(key, obtainedKey);
        }
    }
}
