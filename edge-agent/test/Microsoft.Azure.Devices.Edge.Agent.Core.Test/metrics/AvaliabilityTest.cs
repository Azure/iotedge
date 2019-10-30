// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test.Metrics
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Metrics;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Unit]
    public class AvailabilityTest
    {
        [Fact]
        public void TestAddPoint()
        {
            var systemTime = new Mock<ISystemTime>();
            DateTime fakeTime = DateTime.Now;
            systemTime.Setup(x => x.UtcNow).Returns(() => fakeTime);

            for (int i = 1; i < 100; i++)
            {
                Availability availability = new Availability("Test", systemTime.Object);
                fakeTime = fakeTime.AddMinutes(10);

                for (int j = 0; j < 100; j++)
                {
                    availability.AddPoint(j % i == 0);
                    fakeTime = fakeTime.AddMinutes(10);
                }

                Assert.Equal(TimeSpan.FromMinutes(1000), availability.ExpectedTime);
                Assert.Equal(TimeSpan.FromMinutes(Math.Ceiling(100.0 / i) * 10), availability.RunningTime);
            }
        }

        [Fact]
        public void TestNoPoint()
        {
            var systemTime = new Mock<ISystemTime>();
            DateTime fakeTime = DateTime.Now;
            systemTime.Setup(x => x.UtcNow).Returns(() => fakeTime);

            Availability availability = new Availability("Test", systemTime.Object);
            for (int j = 0; j < 100; j++)
            {
                availability.AddPoint(true);
                fakeTime = fakeTime.AddMinutes(10);
            }

            for (int j = 0; j < 100; j++)
            {
                availability.NoPoint();
                fakeTime = fakeTime.AddMinutes(10);
            }

            for (int j = 0; j < 100; j++)
            {
                availability.AddPoint(false);
                fakeTime = fakeTime.AddMinutes(10);
            }

            // Note approx equal since we lose a bit of information when no point is measured
            TestUtilities.ApproxEqual(TimeSpan.FromMinutes(2000).TotalDays, availability.ExpectedTime.TotalDays, .1);
            TestUtilities.ApproxEqual(TimeSpan.FromMinutes(1000).TotalDays, availability.RunningTime.TotalDays, .1);
        }
    }
}
