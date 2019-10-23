// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util.Test.Common
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Text;
    using Xunit;

    public static class TestUtilities
    {
        public static void ApproxEqual(double expected, double actual, double tolerance)
        {
            Assert.True(Math.Abs(expected - actual) <= tolerance, $"Expected {expected} to be within {tolerance} of {actual}");
        }

        public static void ReflectionEqualCollection<T>(IEnumerable<T> expected, IEnumerable<T> actual)
        {
            var expectedEnum = expected.GetEnumerator();
            var actualEnum = actual.GetEnumerator();

            while (expectedEnum.MoveNext() & actualEnum.MoveNext())
            {
                ReflectionEqual(expectedEnum.Current, actualEnum.Current);
            }

            Assert.False(expectedEnum.MoveNext() || actualEnum.MoveNext());
        }

        public static void ReflectionEqual<T>(T expected, T actual)
        {
            foreach (PropertyInfo property in typeof(T).GetProperties())
            {
                Assert.Equal(property.GetValue(expected, null), property.GetValue(actual, null));
            }
        }
    }
}
