// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service.Test.ScenarioTests
{
    using System;
    using System.Threading;

    using Microsoft.Azure.Devices.Shared;

    public class SimpleTwinCollectionGeneratingStrategy : IDeliverableGeneratingStrategy<TwinCollection>
    {
        private int twinCounter = 0;

        public TwinCollection Next()
        {
            var counter = Interlocked.Increment(ref this.twinCounter);
            var random = new Random(counter * 23);

            return new TwinCollection()
                       {
                            ["counter"] = counter,
                            ["test_prop1"] = Utils.RandomString(random, random.Next(5, 10)),
                            ["test_prop2"] = Utils.RandomString(random, random.Next(5, 10)),
                            ["test_constant1"] = "constant1",
                            ["test_constant2"] = 456,
                            ["$version"] = counter
                       };
        }
    }
}
