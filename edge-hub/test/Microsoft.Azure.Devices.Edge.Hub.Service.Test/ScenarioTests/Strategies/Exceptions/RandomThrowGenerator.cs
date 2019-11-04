// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service.Test.ScenarioTests
{
    using System;
    using System.Threading;

    public class RandomThrowGenerator<T>
        where T : Exception
    {
        private Random rnd = new Random(972736589);
        private double odds = 0.2;
        private int throwCounter = 0;

        public static RandomThrowGenerator<T> Create() => new RandomThrowGenerator<T>();

        public int ThrowCount => Volatile.Read(ref this.throwCounter);

        public RandomThrowGenerator<T> WithOdds(double odds)
        {
            this.odds = odds;
            return this;
        }

        public void ThrowOrNot()
        {
            lock (this.rnd)
            {
                if (this.rnd.NextDouble() < this.odds)
                {
                    this.Throw();
                }
            }
        }

        private void Throw()
        {
            this.throwCounter++; // we should be called from a locked region, no interlocked.increment is needed
            throw Activator.CreateInstance(typeof(T), "Throwing random error for test purposes") as Exception;
        }
    }
}
