using Microsoft.Azure.Devices.Edge.Agent.Core.Metrics;
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
        /// <summary>
        /// This calls all the other tests. Since these are timing based tests, they always take 5 sec,
        /// so it is best to run them in parallel
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task Avaliability()
        {
            await Task.WhenAll(
                TestBasicUptime(),
                TestSkippedMeasure()
            );
        }

        private async Task TestBasicUptime()
        {
            await Task.WhenAll(Enumerable.Range(1, 100).Select(async i =>
            {
                Avaliability avaliability = new Avaliability("Test", "test");
                for (int j = 0; j < 50; j++)
                {
                    avaliability.AddPoint(j % i == 0);
                    await Task.Delay(100);
                }
                TestHelper.ApproxEqual(1.0 / i, avaliability.avaliability, .025);
            }));
        }

        private async Task TestSkippedMeasure()
        {
            await Task.WhenAll(Enumerable.Range(1, 100).Select(async i =>
            {
                Avaliability avaliability = new Avaliability("Test", "test");
                for (int j = 0; j < 20; j++)
                {
                    avaliability.AddPoint(j % i == 0);
                    await Task.Delay(50);
                }
                avaliability.NoPoint();
                await Task.Delay(1000);

                for (int j = 0; j < 20; j++)
                {
                    avaliability.AddPoint(j % i == 0);
                    await Task.Delay(50);
                }
                TestHelper.ApproxEqual(1.0 / i, avaliability.avaliability, .05);

                avaliability.NoPoint();
                await Task.Delay(1000);
                for (int j = 0; j < 20; j++)
                {
                    avaliability.AddPoint(j % i == 0);
                    await Task.Delay(50);
                }
                TestHelper.ApproxEqual(1.0 / i, avaliability.avaliability, .05);
            }));
        }
    }
}
