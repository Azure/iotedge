// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service.Test.ScenarioTests
{
    using System;
    using System.Threading.Tasks;

    public class LinearTimingStrategy : ITimingStrategy
    {
        private int coreDelayMs = 100;
        private int varianceMs = 50;

        private Random random = new Random(532567); // use a constant seed to be more replayable

        public static LinearTimingStrategy Create() => new LinearTimingStrategy();

        public async Task DelayAsync()
        {
            int delayMs;
            lock (this.random)
            {
                delayMs = this.random.Next(this.coreDelayMs - this.varianceMs, this.coreDelayMs + this.varianceMs);
            }

            if (delayMs > 0)
            {
                await Task.Delay(delayMs);
            }
            else
            {
                await Task.Yield();
            }
        }

        public LinearTimingStrategy WithDelay(TimeSpan coreDelay, TimeSpan variance)
        {
            this.coreDelayMs = (int)coreDelay.TotalMilliseconds;
            this.varianceMs = (int)variance.TotalMilliseconds;
            return this;
        }

        public LinearTimingStrategy WithDelay(int coreDelayMs, int varianceMs)
        {
            this.coreDelayMs = coreDelayMs;
            this.varianceMs = varianceMs;
            return this;
        }
    }
}
