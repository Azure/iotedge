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
                Availability availability = new Availability("Test", "test", systemTime.Object);
                // seed avaliability so it knows when to start counting from
                availability.AddPoint(false);
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

            Availability availability = new Availability("Test", "test", systemTime.Object);
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
            TestUtilities.ApproxEqual(.5, availability.RunningTime.TotalSeconds / availability.ExpectedTime.TotalSeconds, .01);
        }

        [Fact]
        public void TestAvailibilityToFromRaw()
        {
            var systemTime = new Mock<ISystemTime>();
            DateTime fakeTime = DateTime.Now;
            systemTime.Setup(x => x.UtcNow).Returns(() => fakeTime);

            var testAvailibilities = Enumerable.Range(0, 10).Select(i =>
            {
                var availibility = new Availability($"Test_{i}", "1", systemTime.Object);
                return availibility;
            }).ToList();

            var rand = new Random();
            foreach (Availability availability in testAvailibilities)
            {
                for (int i = 0; i < rand.Next(100); i++)
                {
                    availability.AddPoint(rand.NextDouble() < .25);
                    fakeTime = fakeTime.AddMinutes(10);
                }
            }

            var rawAvailabilities = testAvailibilities.Select(a => a.ToRaw());
            var reconstructedAvailibilities = rawAvailabilities.Select(ra => new Availability(ra, SystemTime.Instance)).ToList();

            for (int i = 0; i < 10; i++)
            {
                Assert.Equal(testAvailibilities[i].Name, reconstructedAvailibilities[i].Name);
                Assert.Equal(testAvailibilities[i].Version, reconstructedAvailibilities[i].Version);
                Assert.Equal(testAvailibilities[i].RunningTime, reconstructedAvailibilities[i].RunningTime);
                Assert.Equal(testAvailibilities[i].ExpectedTime, reconstructedAvailibilities[i].ExpectedTime);
            }
        }
    }
}
