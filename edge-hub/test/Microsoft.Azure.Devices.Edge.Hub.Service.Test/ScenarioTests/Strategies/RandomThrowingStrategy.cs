namespace Microsoft.Azure.Devices.Edge.Hub.Service.Test.ScenarioTests
{
    using System;
    using System.Collections.Generic;

    using Microsoft.Azure.Devices.Edge.Hub.Core;

    public class RandomThrowingStrategy : IMessageConverter<bool>
    {
        private Random random = new Random(645637);
        private double oddsToThrow = 0.1;

        protected RandomThrowingStrategy()
        {
        }

        public static RandomThrowingStrategy Create()
        {
            return new RandomThrowingStrategy();
        }

        public RandomThrowingStrategy WithOddsToThrow(double oddsToThrow)
        {
            this.oddsToThrow = oddsToThrow;
            return this;
        }

        public bool Convert(IMessage message)
        {
            return this.RollTheDice();
        }

        public bool Convert(IEnumerable<IMessage> message)
        {
            return this.RollTheDice();
        }

        private bool RollTheDice()
        {
            lock (this.random)
            {
                return this.random.NextDouble() < this.oddsToThrow;
            }
        }
    }
}
