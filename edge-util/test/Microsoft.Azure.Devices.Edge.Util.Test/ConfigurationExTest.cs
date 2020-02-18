// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Test
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Extensions.Configuration;
    using Xunit;

    [Unit]
    public class ConfigurationExTest
    {
        const string Key = "value";

        [Fact]
        public void ReturnsParsedTimeSpan()
        {
            TimeSpan expected = TimeSpan.FromMinutes(5);
            IConfiguration config = GetConfiguration(expected.ToString());
            Assert.Equal(expected, config.GetTimeSpan(Key, TimeSpan.Zero));
        }

        [Theory]
        [InlineData("10675199.02:48:06")] // > TimeSpan.MaxValue, causes OverflowException
        [InlineData(" 00:00:00:1500000")] // last ':' should be '.', causes FormatException
        public void ReturnsDefaultWhenTimeSpanStringIsMalformed(string value)
        {
            TimeSpan @default = TimeSpan.FromMinutes(777);
            IConfiguration config = GetConfiguration(value);
            Assert.Equal(@default, config.GetTimeSpan(Key, @default));
        }

        static IConfiguration GetConfiguration(string value)
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string> { { Key, value } })
                .Build();
        }
    }
}
