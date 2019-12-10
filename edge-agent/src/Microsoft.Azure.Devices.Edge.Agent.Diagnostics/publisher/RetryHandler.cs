// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Diagnostics.Publisher
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;

    /// <summary>
    /// This is a wrapper for some function that asynchronously does something that might fail.
    /// If the function returns true, this class will attempt to retry the task.
    /// </summary>
    /// <typeparam name="T">Input data for the wrapped function.</typeparam>
    public class RetryHandler<T> : IDisposable
    {
        readonly Func<T, CancellationToken, Task<bool>> send;
        readonly IKeyValueStore<Guid, T> messagesToRetry;
        readonly int maxRetries;
        readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        // This is intentionally stored in memory. If agent restarts, the retry counts are reset.
        readonly Dictionary<Guid, int> retryTracker = new Dictionary<Guid, int>();
        Task retryTask = null;

        public RetryHandler(Func<T, CancellationToken, Task<bool>> send, IStoreProvider storeProvider, string partition, int maxRetries = 20)
        {
            this.send = send;
            this.messagesToRetry = Preconditions.CheckNotNull(storeProvider.GetEntityStore<Guid, T>(partition), "dataStore");
            this.maxRetries = maxRetries;

            // Check for retrys on startup
            this.messagesToRetry.GetFirstEntry().ContinueWith(result =>
            {
                if (result.Result.HasValue)
                {
                    this.StartRetrying();
                }
            });
        }

        public RetryHandler(Func<T, Task<bool>> send, IStoreProvider storeProvider, string partition, int maxRetries = 20)
            : this((data, _) => send(data), storeProvider, partition, maxRetries = 20)
        {
        }

        public Task Send(T data) => this.Send(data, CancellationToken.None);
        public async Task Send(T data, CancellationToken cancellationToken)
        {
            CancellationToken ct = CancellationTokenSource.CreateLinkedTokenSource(this.cancellationTokenSource.Token, cancellationToken).Token;
            if (await this.send(data, ct))
            {
                await this.messagesToRetry.Put(Guid.NewGuid(), data);
                this.StartRetrying();
            }
        }

        void StartRetrying()
        {
            if (this.retryTask == null)
            {
                this.retryTask = Task.Run(async () =>
                {
                    await this.RetryLoop();
                    this.retryTask = null;
                });
            }
        }

        async Task RetryLoop()
        {
            foreach (TimeSpan sleepTime in this.Backoff())
            {
                await Task.Delay(sleepTime, this.cancellationTokenSource.Token);
                if (await this.Retry())
                {
                    // All messages sent successfully
                    return;
                }
            }
        }

        async Task<bool> Retry()
        {
            bool allCompleatedSuccesfully = true;
            await this.messagesToRetry.IterateBatch(
                1000,
                async (guid, data) =>
                    {
                        if (await this.send(data, this.cancellationTokenSource.Token))
                        {
                            // Message not sent.
                            allCompleatedSuccesfully = false;

                            // If max retrys hit, remove from list
                            if (!this.ShouldRetry(guid))
                            {
                                await this.messagesToRetry.Remove(guid);
                            }
                        }
                        else
                        {
                            // Successfully sent message.
                            await this.messagesToRetry.Remove(guid);
                            this.retryTracker.Remove(guid);
                        }
                    },
                this.cancellationTokenSource.Token);

            return allCompleatedSuccesfully;
        }

        bool ShouldRetry(Guid guid)
        {
            if (this.retryTracker.TryGetValue(guid, out int numRetrys))
            {
                if (numRetrys > this.maxRetries)
                {
                    this.retryTracker.Remove(guid);
                    return false;
                }
                else
                {
                    this.retryTracker[guid] = numRetrys + 1;
                }
            }
            else
            {
                this.retryTracker[guid] = 1;
            }

            return true;
        }

        IEnumerable<TimeSpan> Backoff()
        {
            yield return TimeSpan.FromMinutes(5);
            yield return TimeSpan.FromMinutes(5);
            yield return TimeSpan.FromMinutes(10);
            yield return TimeSpan.FromMinutes(15);

            while (true)
            {
                yield return TimeSpan.FromMinutes(30);
            }
        }

        public void Dispose()
        {
            this.cancellationTokenSource.Dispose();
        }
    }
}
