// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Test.Util
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core.Util;
    using Xunit;

    public class MockSystemTime : ISystemTime
    {
        public DateTime UtcNow { get; set; }

        public MockSystemTime()
            : this(DateTime.UtcNow)
        {
        }

        public MockSystemTime(DateTime now)
        {
            this.UtcNow = now;
        }

        public void Add(TimeSpan timespan)
        {
            this.UtcNow = this.UtcNow.SafeAdd(timespan);
        }
    }

    [Unit]
    [ExcludeFromCodeCoverage]
    public class MockSystemTimeTest
    {
        [Fact]
        public void SmokeTest()
        {
            var time = new MockSystemTime(new DateTime(2017, 5, 23, 0, 0, 0, DateTimeKind.Utc));
            Assert.Equal(new DateTime(2017, 5, 23, 0, 0, 0, DateTimeKind.Utc), time.UtcNow);

            time.Add(TimeSpan.FromMinutes(15));
            Assert.Equal(new DateTime(2017, 5, 23, 0, 15, 0, DateTimeKind.Utc), time.UtcNow);
        }
    }
}