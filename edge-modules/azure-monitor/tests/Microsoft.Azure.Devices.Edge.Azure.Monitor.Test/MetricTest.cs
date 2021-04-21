// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using Xunit;

namespace Microsoft.Azure.Devices.Edge.Azure.Monitor.Test
{
    public class MetricTest
    {
        private readonly static DateTime testTime = DateTime.UnixEpoch;

        private readonly static Dictionary<string, string> someTags = new Dictionary<string, string>() {["label"] = "value", ["label2"] = "", ["__a"] = "度量"};
        private readonly static Dictionary<string, string> someTags2 = new Dictionary<string, string>() {["label"] = "valu", ["label2"] = "", ["__a"] = "度量"};
        private readonly static Dictionary<string, string> someTags3 = new Dictionary<string, string>() {["label"] = "value", ["label2"] = "", ["__a"] = "度量", ["another"] = "度量"};

        [Fact]
        public void TestConstructorThrowsOnNullArguments()
        {
            Assert.Throws<ArgumentNullException>(() => new Metric(testTime, null, 0, new Dictionary<string, string>()));
            new Metric(testTime, "", 0, new Dictionary<string, string>());
            new Metric(testTime, "foobar", 0, new Dictionary<string, string>());
            Assert.Throws<ArgumentNullException>(() => new Metric(testTime, "foobar", 0, null));
        }

        [Fact]
        public void TestWeirdFloatValues()
        {
            // Prometheus allows any float64 values, including infinities and NaNs. Test to make sure they don't throw exceptions.
            Assert.True(new Metric(testTime, "", 0, new Dictionary<string, string>()).Value == 0.0);
            Assert.True(new Metric(testTime, "", Double.MaxValue, new Dictionary<string, string>()).Value == Double.MaxValue);
            Assert.True(new Metric(testTime, "", Double.MinValue, new Dictionary<string, string>()).Value == Double.MinValue);
            Assert.True(new Metric(testTime, "", Double.Epsilon, new Dictionary<string, string>()).Value == Double.Epsilon);
            Assert.True(new Metric(testTime, "", Double.PositiveInfinity, new Dictionary<string, string>()).Value == Double.PositiveInfinity);
            Assert.True(new Metric(testTime, "", Double.NegativeInfinity, new Dictionary<string, string>()).Value == Double.NegativeInfinity);
            Assert.True(Double.IsNaN(new Metric(testTime, "", Double.NaN, new Dictionary<string, string>()).Value));  // (NaN == NaN evaluates to false)
        }

        [Fact]
        public void TestTime()
        {
            // Prometheus allows any float64 values, including infinities and NaNs. Test to make sure they don't throw exceptions.
            Assert.True(new Metric(testTime, "", 0, new Dictionary<string, string>()).TimeGeneratedUtc == testTime);
            DateTime testTime1 = DateTime.ParseExact("2008-06-11T16:11:20.0904778Z", "o", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
            Assert.True(new Metric(testTime1, "", 0, new Dictionary<string, string>()).TimeGeneratedUtc == testTime1);

            // should throw an error if time is not UTC
            DateTime testTime2 = DateTime.Parse("Sun 15 Jun 2008 8:30 AM -05");
            Assert.Throws<ArgumentException>(() => new Metric(testTime2, "", 0, new Dictionary<string, string>()));
        }

        [Fact]
        public void TestEquality()
        {
            DateTime testTime1 = DateTime.ParseExact("2008-06-11T16:11:20.0904778Z", "o", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);

            Assert.True(new Metric(testTime, "", 0.0, someTags2).Equals(new Metric(testTime, "", 0.0, someTags2)));
            Assert.True(new Metric(testTime1, "foo", 0, someTags) == new Metric(testTime1, "foo", 0, someTags));
            Assert.True(new Metric(testTime, "foo", 0, someTags2) == new Metric(testTime, "foo", 0, someTags2));
            Assert.True(new Metric(testTime, "", Double.PositiveInfinity, someTags2) == new Metric(testTime, "", Double.PositiveInfinity, someTags2));
            Assert.True(new Metric(testTime, "", Double.NaN, someTags2) == new Metric(testTime, "", Double.NaN, someTags2));

            Assert.False(new Metric(testTime, "foo", Double.NaN, someTags2) == new Metric(testTime1, "foo", 0.0, someTags2));
            Assert.False(new Metric(testTime, "foo", 0, someTags) == new Metric(testTime, "fooo", 0, someTags));
            Assert.False(new Metric(testTime, "foo", 0, someTags2) == new Metric(testTime1, "foo", 0, someTags2));
            Assert.False(new Metric(testTime, "", Double.PositiveInfinity, someTags2) == new Metric(testTime, "", Double.MaxValue, someTags2));
            Assert.False(new Metric(testTime, "", Double.NaN, someTags2) == new Metric(testTime, "", 0.0, someTags2));
            Assert.False(new Metric(testTime, "", 0.0, someTags) == new Metric(testTime, "", 0.0, someTags2));
            Assert.False(new Metric(testTime, "", 0.0, someTags) == new Metric(testTime, "", 0.0, someTags3));
            Assert.False(new Metric(testTime, "", 0.0, someTags2) == new Metric(testTime, "", 0.0, someTags3));
        }
    }
}
