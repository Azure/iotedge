// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Test.Concurrency
{
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    public class AtomicBooleanTest
    {
        [Fact]
        [Unit]
        public void TestDefault()
        {
            var b = new AtomicBoolean();
            Assert.Equal(false, b.Get());
            Assert.Equal(false, b);

            var b2 = new AtomicBoolean(true);
            Assert.Equal(true, b2.Get());
            Assert.Equal(true, b2);
        }

        [Fact]
        [Unit]
        public void TestSet()
        {
            var b = new AtomicBoolean(true);
            b.Set(false);
            Assert.Equal(false, b.Get());

            b.Set(true);
            Assert.Equal(true, b.Get());
        }

        [Fact]
        [Unit]
        public void TestGetAndSet()
        {
            var b1 = new AtomicBoolean(true);
            bool result = b1.GetAndSet(false);
            Assert.Equal(true, result);
            Assert.Equal(false, b1.Get());
        }

        [Fact]
        [Unit]
        public void TestCompareAndSet()
        {
            var b1 = new AtomicBoolean(true);
            bool result = b1.CompareAndSet(true, false);
            Assert.Equal(true, result);
            Assert.Equal(false, b1.Get());

            result = b1.CompareAndSet(true, true);
            Assert.Equal(false, result);
            Assert.Equal(false, b1.Get());

            result = b1.CompareAndSet(false, true);
            Assert.Equal(true, result);
            Assert.Equal(true, b1.Get());
        }
    }
}