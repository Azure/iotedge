// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util.Test
{
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Unit]
    public class VersionInfoTest
    {
        [Fact]
        public void GetVersionInfoTest()
        {
            VersionInfo versionInfo = VersionInfo.Get("testVersionInfo.json");
            Assert.NotNull(versionInfo);
            Assert.NotEqual(versionInfo, VersionInfo.Empty);
            Assert.Equal("1.0", versionInfo.Version);
            Assert.Equal("7238638", versionInfo.Build);
            Assert.Equal("fc9f2dafaf93cac936ef756bb37efeaa58688914", versionInfo.Commit);
            Assert.Equal("1.0.7238638 (fc9f2dafaf93cac936ef756bb37efeaa58688914)", versionInfo.ToString());
        }

        [Fact]
        public void GetVersionInfoInvalidTest()
        {
            VersionInfo versionInfo = VersionInfo.Get("dummyVersionInfo.json");
            Assert.NotNull(versionInfo);
            Assert.Equal(versionInfo, VersionInfo.Empty);
            Assert.Equal(string.Empty, versionInfo.ToString());
        }
    }
}
