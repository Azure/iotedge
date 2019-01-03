// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Test
{
    using System;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Unit]
    public class SystemEvironmentTest
    {
        [Fact]
        public void SystemEnvironmentTest()
        {
            var env = new SystemEnvironment();

            Assert.True(Environment.Is64BitOperatingSystem != env.Is32BitOperatingSystem);
            Assert.True(Environment.Is64BitProcess != env.Is32BitProcess);
        }
    }
}
