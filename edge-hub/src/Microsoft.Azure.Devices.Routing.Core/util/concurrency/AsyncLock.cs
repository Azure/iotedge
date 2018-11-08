// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core.Util.Concurrency
{
    // Code ported from http://blogs.msdn.com/b/pfxteam/archive/2012/02/12/10266988.aspx
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public sealed class AsyncLock : IDisposable
    {
        readonly Task<Releaser> releaser;
        readonly SemaphoreSlim semaphore;

        public AsyncLock()
            : this(1)
        {
        }

        public AsyncLock(int maximumConcurrency)
        {
            this.releaser = Task.FromResult(new Releaser(this));
            this.semaphore = new SemaphoreSlim(maximumConcurrency, maximumConcurrency);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.semaphore.Dispose();
        }

        public Task<Releaser> LockAsync() => this.LockAsync(CancellationToken.None);

        public Task<Releaser> LockAsync(CancellationToken token)
        {
            Task wait = this.semaphore.WaitAsync(token);
            return wait.Status == TaskStatus.RanToCompletion
                ? this.releaser
                : wait.ContinueWith(
                    (_, state) => new Releaser((AsyncLock)state),
                    this,
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnRanToCompletion,
                    TaskScheduler.Default);
        }

        public struct Releaser : IDisposable
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
