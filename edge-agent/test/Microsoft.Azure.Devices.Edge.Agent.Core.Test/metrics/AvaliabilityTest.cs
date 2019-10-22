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
        public void TestBasicUptime()
        {
            var systemTime = new Mock<ISystemTime>();
            DateTime fakeTime = DateTime.Now;
            systemTime.Setup(x => x.UtcNow).Returns(() => fakeTime);

            for (int i = 1; i < 100; i++)
            {
                Availability availability = new Availability("Test", "test", systemTime.Object);
                for (int j = 0; j < 100; j++)
                {
                    availability.AddPoint(j % i == 0);
                    fakeTime = fakeTime.AddMinutes(10);
                }

                TestUtilities.ApproxEqual(1.0 / i, availability.AvailabilityRatio, .01);
            }
        }

        [Fact]
        public void TestSkippedMeasure()
        {
            var systemTime = new Mock<ISystemTime>();
            DateTime fakeTime = DateTime.Now;
            systemTime.Setup(x => x.UtcNow).Returns(() => fakeTime);

            for (int i = 1; i < 100; i++)
            {
                Availability availability = new Availability("Test", "test", systemTime.Object);
                for (int j = 0; j < 20; j++)
                {
                    availability.AddPoint(j % i == 0);
                    fakeTime = fakeTime.AddMinutes(10);
                }

                availability.NoPoint();
                fakeTime = fakeTime.AddMinutes(10);

                for (int j = 0; j < 20; j++)
                {
                    availability.AddPoint(j % i == 0);
                    fakeTime = fakeTime.AddMinutes(10);
                }

                TestUtilities.ApproxEqual(1.0 / i, availability.AvailabilityRatio, .05);

                availability.NoPoint();
                fakeTime = fakeTime.AddMinutes(1000);
                for (int j = 0; j < 20; j++)
                {
                    availability.AddPoint(j % i == 0);
                    fakeTime = fakeTime.AddMinutes(10);
                }

                TestUtilities.ApproxEqual(1.0 / i, availability.AvailabilityRatio, .05);
            }
        }

        [Fact]
        public void TestWeeklyUptime()
        {
            var systemTime = new Mock<ISystemTime>();
            DateTime fakeTime = DateTime.Now.Date;
            systemTime.Setup(x => x.UtcNow).Returns(() => fakeTime);

            /* up for 10 days */
            WeeklyAvailability availability = new WeeklyAvailability("Test", "test", systemTime.Object);
            for (int i = 0; i < 10; i++)
            {
                for (int j = 0; j < 24 * 6; j++)
                {
                    availability.AddPoint(true);
                    fakeTime = fakeTime.AddMinutes(10);
                }

                TestUtilities.ApproxEqual(1, availability.AvailabilityRatio, .01);
            }

            /* down for 10 days */
            for (int i = 1; i < 10; i++)
            {
                for (int j = 0; j < 24 * 6; j++)
                {
                    availability.AddPoint(false);
                    fakeTime = fakeTime.AddMinutes(10);
                }

                double expected = Math.Max(7.0 - i, 0.0) / 7.0;
                TestUtilities.ApproxEqual(expected, availability.AvailabilityRatio, .01);
            }

            /* up for 10 days */
            for (int i = 1; i < 10; i++)
            {
                for (int j = 0; j < 24 * 6; j++)
                {
                    availability.AddPoint(true);
                    fakeTime = fakeTime.AddMinutes(10);
                }

                double expected = Math.Min(i, 7.0) / 7.0;
                TestUtilities.ApproxEqual(expected, availability.AvailabilityRatio, .01);
            }

            /* down for half a day */
            for (int i = 0; i < 12 * 6; i++)
            {
                availability.AddPoint(false);
                fakeTime = fakeTime.AddMinutes(10);
            }

            TestUtilities.ApproxEqual(6.5 / 7.0, availability.AvailabilityRatio, .01);
            for (int i = 0; i < 12 * 6; i++)
            {
                availability.AddPoint(true);
                fakeTime = fakeTime.AddMinutes(10);
            }

            TestUtilities.ApproxEqual(6.5 / 7.0, availability.AvailabilityRatio, .01);

            /* up for another 6 days */
            for (int i = 0; i < 6; i++)
            {
                for (int j = 0; j < 24 * 6; j++)
                {
                    availability.AddPoint(true);
                    fakeTime = fakeTime.AddMinutes(10);
                }

                TestUtilities.ApproxEqual(6.5 / 7.0, availability.AvailabilityRatio, .01);
            }

            /* avalability goes back to max */
            for (int i = 0; i < 10; i++)
            {
                for (int j = 0; j < 24 * 6; j++)
                {
                    availability.AddPoint(true);
                    fakeTime = fakeTime.AddMinutes(10);
                }

                TestUtilities.ApproxEqual(1, availability.AvailabilityRatio, .01);
            }
        }

        [Fact]
        public void TestAvailibilityToFromRaw()
        {
            var testAvailibilities = Enumerable.Range(0, 10).Select(i =>
            {
                var availibility = new Availability($"Test_{i}", "1", SystemTime.Instance);
                availibility.TotalTime = TimeSpan.FromHours(10);
                availibility.Uptime = TimeSpan.FromHours(i);

                return availibility;
            }).ToList();

            var rawAvailabilities = testAvailibilities.Select(a => a.ToRaw());
            var reconstructedAvailibilities = rawAvailabilities.Select(ra => new Availability(ra, SystemTime.Instance)).ToList();

            for (int i = 0; i < 10; i++)
            {
                Assert.Equal(i / 10.0, testAvailibilities[i].AvailabilityRatio);
                Assert.Equal(i / 10.0, reconstructedAvailibilities[i].AvailabilityRatio);

                Assert.Equal(testAvailibilities[i].Name, reconstructedAvailibilities[i].Name);
                Assert.Equal(testAvailibilities[i].Version, reconstructedAvailibilities[i].Version);
            }
        }

        [Fact]
        public void TestWeeklyAvailibilityToFromRaw()
        {
            var systemTime = new Mock<ISystemTime>();
            DateTime fakeTime = DateTime.Now.Date;
            systemTime.Setup(x => x.UtcNow).Returns(() => fakeTime);

            var testAvailibilities = Enumerable.Range(1, 10).Select(i => new WeeklyAvailability($"Test_{i}", "1", systemTime.Object)).ToList();

            for (int i = 0; i < 24 * 7; i++)
            {
                for (int j = 0; j < testAvailibilities.Count; j++)
                {
                    testAvailibilities[j].AddPoint(i % (j + 1) == 0);
                    fakeTime = fakeTime.AddMinutes(10);
                }
            }

            var rawAvailabilities = testAvailibilities.Select(a => a.ToRaw());
            var reconstructedAvailibilities = rawAvailabilities.Select(ra => new WeeklyAvailability(ra, systemTime.Object)).ToList();

            for (int i = 0; i < 10; i++)
            {
                double expected = 1.0 / (i + 1);
                TestUtilities.ApproxEqual(expected, testAvailibilities[i].AvailabilityRatio, .05);
                TestUtilities.ApproxEqual(expected, reconstructedAvailibilities[i].AvailabilityRatio, .05);

                Assert.Equal(testAvailibilities[i].Name, reconstructedAvailibilities[i].Name);
                Assert.Equal(testAvailibilities[i].Version, reconstructedAvailibilities[i].Version);
            }
        }
    }
}
