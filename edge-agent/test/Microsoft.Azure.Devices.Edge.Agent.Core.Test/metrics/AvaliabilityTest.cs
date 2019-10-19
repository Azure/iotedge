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
    public class AvaliabilityTest
    {
        [Fact]
        public void TestBasicUptime()
        {
            var systemTime = new Mock<ISystemTime>();
            DateTime fakeTime = DateTime.Now;
            systemTime.Setup(x => x.UtcNow).Returns(() => fakeTime);

            for (int i = 1; i < 100; i++)
            {
                Avaliability avaliability = new Avaliability("Test", "test", systemTime.Object);
                for (int j = 0; j < 100; j++)
                {
                    avaliability.AddPoint(j % i == 0);
                    fakeTime = fakeTime.AddMinutes(10);
                }

                TestUtilities.ApproxEqual(1.0 / i, avaliability.Avaliability1, .01);
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
                Avaliability avaliability = new Avaliability("Test", "test", systemTime.Object);
                for (int j = 0; j < 20; j++)
                {
                    avaliability.AddPoint(j % i == 0);
                    fakeTime = fakeTime.AddMinutes(10);
                }

                avaliability.NoPoint();
                fakeTime = fakeTime.AddMinutes(10);

                for (int j = 0; j < 20; j++)
                {
                    avaliability.AddPoint(j % i == 0);
                    fakeTime = fakeTime.AddMinutes(10);
                }

                TestUtilities.ApproxEqual(1.0 / i, avaliability.Avaliability1, .05);

                avaliability.NoPoint();
                fakeTime = fakeTime.AddMinutes(1000);
                for (int j = 0; j < 20; j++)
                {
                    avaliability.AddPoint(j % i == 0);
                    fakeTime = fakeTime.AddMinutes(10);
                }

                TestUtilities.ApproxEqual(1.0 / i, avaliability.Avaliability1, .05);
            }
        }
    }
}
