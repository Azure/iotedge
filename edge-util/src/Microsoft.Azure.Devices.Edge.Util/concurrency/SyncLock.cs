// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Concurrency
{
    using System;
    using System.Threading;

    public class SyncLock : IDisposable
    {
        readonly object locker;

        SyncLock(object locker)
        {
            this.locker = locker;
        }

        public static SyncLock Lock(object locker, TimeSpan timeout)
        {
            if (Monitor.TryEnter(locker, timeout))
                return new SyncLock(locker);
            else
                throw new TimeoutException("Failed to acquire the lock.");
        }

        public void Dispose() => Monitor.Exit(this.locker);
    }
}
