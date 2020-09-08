// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service.Test.ScenarioTests
{
    using System.Threading;
    using System.Threading.Tasks;

    public class EventBasedTimingStrategy : ITimingStrategy
    {
        private WaitHandle trigger = CancellationToken.None.WaitHandle;

        public static EventBasedTimingStrategy Create() => new EventBasedTimingStrategy();

        public EventBasedTimingStrategy WithTrigger(WaitHandle trigger)
        {
            this.trigger = trigger;
            return this;
        }

        public Task DelayAsync()
        {
            var tcs = new TaskCompletionSource<bool>();
            var rwh = ThreadPool.RegisterWaitForSingleObject(this.trigger, (_, __) => tcs.TrySetResult(true),  null, -1, true);
            tcs.Task.ContinueWith(_ => rwh.Unregister(null));

            return tcs.Task;
        }
    }
}
