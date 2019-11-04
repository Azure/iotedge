// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service.Test.ScenarioTests
{
    using System;
    using System.Threading.Tasks;

    public class RandomLaggingTimingStrategy : ITimingStrategy
    {
        private ITimingStrategy baseStrategy = new LinearTimingStrategy();
        private double oddsToGetStuck = 0.1;
        private int coreDelayMs = 80000;
        private int varianceMs = 20000;

        private Random random = new Random(278934);

        public static RandomLaggingTimingStrategy Create() => new RandomLaggingTimingStrategy();

        public RandomLaggingTimingStrategy WithOddsToGetStuck(double oddsToGetStuck)
        {
            this.oddsToGetStuck = oddsToGetStuck;
            return this;
        }

        public RandomLaggingTimingStrategy WithDelay(TimeSpan coreDelay, TimeSpan variance)
        {
            this.coreDelayMs = (int)coreDelay.TotalMilliseconds;
            this.varianceMs = (int)variance.TotalMilliseconds;
            return this;
        }

        public RandomLaggingTimingStrategy WithDelay(int coreDelayMs, int varianceMs)
        {
            this.coreDelayMs = coreDelayMs;
            this.varianceMs = varianceMs;
            return this;
        }

        public RandomLaggingTimingStrategy WithBaseStrategy<T>(Func<T, T> baseStrategyDecorator)
            where T : ITimingStrategy, new()
        {
            this.baseStrategy = baseStrategyDecorator(new T());
            return this;
        }

        public Task DelayAsync() => this.RollTheDice() ? this.GetStuck() : this.baseStrategy.DelayAsync();

        private Task GetStuck()
        {
            int delayMs;
            lock (this.random)
            {
                delayMs = this.random.Next(this.coreDelayMs - this.varianceMs, this.coreDelayMs + this.varianceMs);
            }

            return Task.Delay(delayMs);
        }

        private bool RollTheDice()
        {
            lock (this.random)
            {
                return this.random.NextDouble() < this.oddsToGetStuck;
            }
        }
    }
}
