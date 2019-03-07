// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Test.Util.Concurrency
{
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core.Util.Concurrency;
    using Xunit;

    public class AtomicBooleanTest
    {
        [Fact]
        [Unit]
        public void TestDefault()
        {
            var b = new AtomicBoolean();
            Assert.False(b.Get());
            Assert.False(b);

            var b2 = new AtomicBoolean(true);
            Assert.True(b2.Get());
            Assert.True(b2);
        }

        [Fact]
        [Unit]
        public void TestSet()
        {
            var b = new AtomicBoolean(true);
            b.Set(false);
            Assert.False(b.Get());

            b.Set(true);
            Assert.True(b.Get());
        }

        [Fact]
        [Unit]
        public void TestGetAndSet()
        {
            var b1 = new AtomicBoolean(true);
            bool result = b1.GetAndSet(false);
            Assert.True(result);
            Assert.False(b1.Get());
        }

        [Fact]
        [Unit]
        public void TestCompareAndSet()
        {
            var b1 = new AtomicBoolean(true);
            bool result = b1.CompareAndSet(true, false);
            Assert.True(result);
            Assert.False(b1.Get());

            result = b1.CompareAndSet(true, true);
            Assert.False(result);
            Assert.False(b1.Get());

            result = b1.CompareAndSet(false, true);
            Assert.True(result);
            Assert.True(b1.Get());
        }
    }
}
