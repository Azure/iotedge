// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util.Test.Concurrency
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    public class AsyncLockTest
    {
        [Fact]
        [Unit]
        public void TestUnlockedPermitsLockSynchronously()
        {
            var mutex = new AsyncLock();
            Task<AsyncLock.Releaser> lockTask = mutex.LockAsync();

            Assert.True(lockTask.IsCompleted);
            Assert.False(lockTask.IsFaulted);
            Assert.False(lockTask.IsCanceled);
        }

        [Fact]
        [Unit]
        public async Task TestLockedPreventsLockUntilUnlocked()
        {
            var mutex = new AsyncLock();
            var locked = new TaskCompletionSource<bool>();
            var cont = new TaskCompletionSource<bool>();

            Task t1 = Task.Run(async () =>
            {
                using (await mutex.LockAsync())
                {
                    locked.SetResult(true);
                    await cont.Task;
                }
            });
            await locked.Task;

            Task<Task<AsyncLock.Releaser>> t2Start = Task.Factory.StartNew(async () => await mutex.LockAsync());
            Task<AsyncLock.Releaser> t2 = await t2Start;

            Assert.False(t2.IsCompleted);
            cont.SetResult(true);
            await t2;
            await t1;
        }

        [Fact]
        [Unit]
        public async Task TestDoubleDisposeOnlyPermitsOneTask()
        {
            var mutex = new AsyncLock();
            var t1HasLock = new TaskCompletionSource<bool>();
            var t1Continue = new TaskCompletionSource<bool>();

            await Task.Run(async () =>
            {
                AsyncLock.Releaser key = await mutex.LockAsync();
                key.Dispose();
                key.Dispose();
            });

            Task t1 = Task.Run(async () =>
            {
                using (await mutex.LockAsync())
                {
                    t1HasLock.SetResult(true);
                    await t1Continue.Task;
                }
            });
            await t1HasLock.Task;

            Task<Task<AsyncLock.Releaser>> task2Start = Task.Factory.StartNew(async () => await mutex.LockAsync());
            Task<AsyncLock.Releaser> t2 = await task2Start;

            Assert.False(t2.IsCompleted);
            t1Continue.SetResult(true);
            await t2;
            await t1;
        }

        [Fact]
        [Unit]
        public async Task TestCancellation()
        {
            var mutex = new AsyncLock();
            var cts = new CancellationTokenSource();

            await mutex.LockAsync(cts.Token);
            Task<AsyncLock.Releaser> canceled = mutex.LockAsync(cts.Token);
            cts.Cancel();

            await Assert.ThrowsAsync<TaskCanceledException>(() => canceled);
            mutex.Dispose();
        }

        [Fact]
        [Unit]
        public async Task TestThrowsIfTokenCancelled()
        {
            var mutex = new AsyncLock();
            var cts = new CancellationTokenSource();
            cts.Cancel();

            async Task F1()
            {
                using (await mutex.LockAsync(cts.Token))
                {
                }
            }

            await Assert.ThrowsAsync<TaskCanceledException>(F1);
        }
    }
}