namespace Microsoft.Azure.Devices.Edge.Util.Test
{
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Xunit;

    public class LinqExTest
    {
        [Fact]
        [Unit]
        public void TestThrowsAreIgnored()
        {
            IEnumerable<int> results = Enumerable
                .Range(1, 5)
                .Select(n => n % 2 == 0 ? n / 0 : n)
                .IgnoreExceptions<int, DivideByZeroException>();

            // 'results' will have only odd numbers because a DivideByZeroException
            // would have been thrown and ignored for all even numbers
            Assert.All(results, n => Assert.True(n % 2 != 0));
        }

        [Fact]
        [Unit]
        public void TestExceptionActionIsCalled()
        {
            int callCount = 0;
            IEnumerable<int> results = Enumerable
                .Range(1, 5)
                .Select(n => n % 2 == 0 ? n / 0 : n)
                .IgnoreExceptions<int, DivideByZeroException>(ex => callCount++);

            // 'results' will have only odd numbers because a DivideByZeroException
            // would have been thrown and ignored for all even numbers
            Assert.All(results, n => Assert.True(n % 2 != 0));
            Assert.Equal(2, callCount);
        }
    }
}
