// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Diagnostics.Publisher
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;

    public class RetryHandler<T>
    {
        readonly Func<T, Task<bool>> send;
        readonly IKeyValueStore<Guid, T> messagesToRetry;
        readonly int maxRetries;
        readonly Dictionary<Guid, int> retryTracker = new Dictionary<Guid, int>();

        Task retryTask = null;

        public RetryHandler(Func<T, Task<bool>> send, IStoreProvider storeProvider, int maxRetries = 20)
        {
            this.send = send;
            this.messagesToRetry = Preconditions.CheckNotNull(storeProvider.GetEntityStore<Guid, T>("Diagnostic Messages"), "dataStore");
            this.maxRetries = maxRetries;
        }

        public async Task Send(T data)
        {
            if (await this.send(data))
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
                    foreach (TimeSpan sleepTime in this.Backoff())
                    {
                        await Task.Delay(sleepTime);
                        if (await this.Retry())
                        {
                            break;
                        }
                    }

                    this.retryTask = null;
                });
            }
        }

        async Task<bool> Retry()
        {
            bool allCompleatedSuccesfully = true;
            await this.messagesToRetry.IterateBatch(1000, async (guid, data) =>
            {
                if (await this.send(data))
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
            });

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
    }
}
