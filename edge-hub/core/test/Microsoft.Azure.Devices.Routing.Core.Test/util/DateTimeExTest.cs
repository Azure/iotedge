// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Test.Util
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core.Util;
    using Xunit;

    public class DateTimeExTest
    {
        [Theory]
        [Unit]
        [MemberData(nameof(TestDataSource.TestData), MemberType = typeof(TestDataSource))]
        public void TestSafeAdd(DateTime start, TimeSpan period, DateTime expected)
        {
            DateTime result = start.SafeAdd(period);
            Assert.Equal(expected, result);
        }

        static class TestDataSource
        {
            static readonly IList<object[]> Data = new List<object[]>
            {
                new object[] { new DateTime(2016, 3, 27), TimeSpan.FromDays(1), new DateTime(2016, 3, 28) },
                new object[] { new DateTime(2016, 3, 27), TimeSpan.FromDays(-1), new DateTime(2016, 3, 26) },
                new object[] { new DateTime(2016, 3, 27), TimeSpan.MaxValue, DateTime.MaxValue },
                new object[] { new DateTime(2016, 3, 27), TimeSpan.MinValue, DateTime.MinValue },
                new object[] { DateTime.MaxValue, TimeSpan.MaxValue, DateTime.MaxValue },
                new object[] { DateTime.MaxValue, TimeSpan.MinValue, DateTime.MinValue },
                new object[] { DateTime.MinValue, TimeSpan.MinValue, DateTime.MinValue },
                new object[] { DateTime.MinValue, TimeSpan.MaxValue, DateTime.MaxValue },
            };

            public static IEnumerable<object[]> TestData => Data;
        }
    }
}
