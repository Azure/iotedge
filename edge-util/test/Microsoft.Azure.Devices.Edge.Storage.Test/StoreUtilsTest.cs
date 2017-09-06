// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Storage.Test
{
    using Microsoft.Azure.Devices.Edge.Storage;
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
    }
}
