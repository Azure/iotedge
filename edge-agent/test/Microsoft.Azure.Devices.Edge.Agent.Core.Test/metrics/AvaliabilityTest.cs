using Microsoft.Azure.Devices.Edge.Util.Test.Common;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test.Metrics
{
    [Unit]
    public class AvaliabilityTest
    {
        [Fact]
        public async Task TestBasicUptime()
        {
            await Task.WhenAll(Enumerable.Range(1, 100).Select(async i =>
            {
                UptimeMetrics.Avaliability avaliability = new UptimeMetrics.Avaliability("Test", "test");
                for (int j = 0; j < 20; j++)
                {
                    avaliability.AddPoint(j % i == 0);
                    await Task.Delay(100);
                }
                ApproxEqual(1.0 / i, avaliability.avaliability, .05);
            }));
        }
        private static void ApproxEqual(double expected, double actual, double tolerance)
        {
            Assert.True(Math.Abs(expected - actual) <= tolerance, $"Expected {expected} to be within {tolerance} of {actual}");
        }
    }
}
