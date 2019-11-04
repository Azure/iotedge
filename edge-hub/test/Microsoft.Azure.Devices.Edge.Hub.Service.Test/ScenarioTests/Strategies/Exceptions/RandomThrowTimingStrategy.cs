// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service.Test.ScenarioTests
{
    using System;
    using System.Collections.Generic;

    using Microsoft.Azure.Devices.Edge.Hub.Core;

    public class RandomThrowTimingStrategy : IMessageConverter<bool>
    {
        private Random random = new Random(645637);
        private double oddsToThrow = 0.1;

        public RandomThrowTimingStrategy()
        {
        }

        public static RandomThrowTimingStrategy Create() => new RandomThrowTimingStrategy();

        public RandomThrowTimingStrategy WithOddsToThrow(double oddsToThrow)
        {
            this.oddsToThrow = oddsToThrow;
            return this;
        }

        public bool Convert(IMessage message) => this.RollTheDice();
        public bool Convert(IEnumerable<IMessage> message) => this.RollTheDice();
        public bool Convert(Client.Message message) => this.RollTheDice();
        public bool Convert(IEnumerable<Client.Message> messages) => this.RollTheDice();

        private bool RollTheDice()
        {
            lock (this.random)
            {
                return this.random.NextDouble() < this.oddsToThrow;
            }
        }
    }
}
