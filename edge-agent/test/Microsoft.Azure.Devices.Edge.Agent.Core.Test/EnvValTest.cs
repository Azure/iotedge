// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test
{
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    public class EnvValTest
    {
        [Fact]
        [Unit]
        public void CreateTest()
        {
            Assert.NotNull(new EnvVal("Foo"));
            Assert.NotNull(new EnvVal(null));
            Assert.NotNull(new EnvVal(string.Empty));
        }

        [Fact]
        [Unit]
        public void EqualsTest()
        {
            var e1 = new EnvVal("Foo");
            var e2 = new EnvVal("Foo");
            var e3 = new EnvVal("Bar");
            Assert.Equal(e1, e2);
            Assert.NotEqual(e1, e3);
        }
    }
}
