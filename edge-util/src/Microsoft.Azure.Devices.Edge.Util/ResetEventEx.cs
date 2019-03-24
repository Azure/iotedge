// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using System.Threading.Tasks;
    using Nito.AsyncEx;

    public static class ResetEventEx
    {
        public static Task<bool> WaitAsync(this AsyncManualResetEvent resetEvent, TimeSpan timeout)
            => WaitAsync(resetEvent.WaitAsync(), timeout);

        public static Task<bool> WaitAsync(this AsyncAutoResetEvent resetEvent, TimeSpan timeout)
            => WaitAsync(resetEvent.WaitAsync(), timeout);

        static async Task<bool> WaitAsync(Task waitTask, TimeSpan timeout)
        {
            Task timeoutTask = Task.Delay(timeout);
            Task completedTask = await Task.WhenAny(waitTask, timeoutTask);
            return completedTask == waitTask;
        }
    }
}
