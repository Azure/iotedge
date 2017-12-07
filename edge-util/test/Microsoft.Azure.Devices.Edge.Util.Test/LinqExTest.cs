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

        [Fact]
        [Unit]
        public void TestRemoveIntersectionKeysDefault()
        {
            var seq1 = new[]
            {
                "k1=v1",
                "k2=v2",
                "k3=v3",
                "k4=v4",
                "k5=v5",
                "k6=v6"
            };

            var seq2 = new[]
            {
                "pk1=v1",
                "pk2=v2",
                "k6=v6",
                "k2=v21",
                "pk3=v3",
                "k5=v5",
                "pk4=v4",
                "k3=v32"
            };

            IEnumerable<string> result1 = seq1.RemoveIntersectionKeys(seq2);
            Assert.True(result1.SequenceEqual(new[]
            {
                "k1=v1",
                "k4=v4"
            }));

            IEnumerable<string> result2 = seq2.RemoveIntersectionKeys(seq1);
            Assert.True(result2.SequenceEqual(new[]
            {
                "pk1=v1",
                "pk2=v2",
                "pk3=v3",
                "pk4=v4"
            }));
        }

        [Fact]
        [Unit]
        public void TestRemoveIntersectionKeysWithKeySelector()
        {
            var seq1 = new[]
            {
                "k1=v1",
                "k2=v2",
                "k3=v3",
                "k4=v4",
                "k5=v5",
                "k6=v6"
            };

            var seq2 = new[]
            {
                "pk1=v1",
                "pk2=v2",
                "k6=v6",
                "k2=v21",
                "pk3=v3",
                "k5=v5",
                "pk4=v4",
                "k3=v32"
            };

            Func<string, string> keySelector = s => s;

            IEnumerable<string> result1 = seq1.RemoveIntersectionKeys(seq2, keySelector);
            Assert.True(result1.SequenceEqual(new[]
            {
                "k1=v1",
                "k2=v2",
                "k3=v3",
                "k4=v4"
            }));

            IEnumerable<string> result2 = seq2.RemoveIntersectionKeys(seq1, keySelector);
            Assert.True(result2.SequenceEqual(new[]
            {
                "pk1=v1",
                "pk2=v2",
                "k2=v21",
                "pk3=v3",
                "pk4=v4",
                "k3=v32"
            }));
        }
    }
}
