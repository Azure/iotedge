// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service.Test.ScenarioTests
{
    using System.Threading.Tasks;

    public class NoWaitTimingStrategy : ITimingStrategy
    {
        public static NoWaitTimingStrategy Create() => new NoWaitTimingStrategy();

        public async Task DelayAsync()
        {
            await Task.Yield();
            return;
        }
    }
}
