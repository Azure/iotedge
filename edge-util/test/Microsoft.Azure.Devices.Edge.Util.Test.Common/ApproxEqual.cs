using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace Microsoft.Azure.Devices.Edge.Util.Test.Common
{
    public static class TestHelper
    {
        public static void ApproxEqual(double expected, double actual, double tolerance)
        {
            Assert.True(Math.Abs(expected - actual) <= tolerance, $"Expected {expected} to be within {tolerance} of {actual}");
        }
    }
}
