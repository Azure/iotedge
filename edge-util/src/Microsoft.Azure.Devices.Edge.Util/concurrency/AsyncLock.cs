// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Concurrency
{
    // Code ported from http://blogs.msdn.com/b/pfxteam/archive/2012/02/12/10266988.aspx
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public sealed class AsyncLock : IDisposable
    {
        readonly SemaphoreSlim semaphore;

        public AsyncLock()
            : this(1)
        {
        }

        public AsyncLock(int maximumConcurrency)
        {
            this.semaphore = new SemaphoreSlim(maximumConcurrency, maximumConcurrency);
        }

        public Task<IDisposable> LockAsync() => this.LockAsync(CancellationToken.None);

        public Task<IDisposable> LockAsync(CancellationToken token)
        {
            Task wait = this.semaphore.WaitAsync(token);
            return wait.Status == TaskStatus.RanToCompletion
                ? Task.FromResult<IDisposable>(new Releaser(this))
                : wait.ContinueWith<IDisposable>(
                    (_, state) => new Releaser((AsyncLock)state),
                    this,
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnRanToCompletion,
                    TaskScheduler.Default);
        }

        /// <inheritdoc />
        public void Dispose() => this.semaphore.Dispose();

        private class Releaser : IDisposable
        {
            readonly AsyncLock toRelease;
            int disposed;

            public Releaser(AsyncLock toRelease)
            {
                Preconditions.CheckNotNull(toRelease);
                this.toRelease = toRelease;
                this.disposed = 0;
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref this.disposed, 1) == 0)
                {
                    this.toRelease.semaphore.Release();
                }
            }
        }
    }
}
