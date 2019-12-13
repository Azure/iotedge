// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util.Retrying
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Wraps an async function and attempts to retry the wrapped function if it fails
    /// as determined by a shouldRety function.
    /// </summary>
    /// <typeparam name="TParam">Parameter of the wrapped function. Use tupples if multiple inputs are desired.</typeparam>
    /// <typeparam name="TResult">Result of wrapped function.</typeparam>
    public class RetryWithBackoff<TParam, TResult> : IDisposable
    {
        readonly Func<TParam, CancellationToken, Task<TResult>> doWorkAsync;
        readonly Func<TResult, bool> shouldRetry;
        readonly IBackoff backoff;
        readonly IDictionary<Guid, TParam> thingsToRetry;
        readonly string name;
        readonly double maxRetries;

        readonly ILogger log = Logger.Factory.CreateLogger<RetryWithBackoff<TParam, TResult>>();
        readonly DefaultDictionary<Guid, int> numRetries = new DefaultDictionary<Guid, int>(_ => 1);
        readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        Task retryTask;

        public RetryWithBackoff(Func<TParam, CancellationToken, Task<TResult>> doWorkAsync, Func<TResult, bool> shouldRetry, IBackoff backoff, IDictionary<Guid, TParam> retryStorage, string name, double maxRetries = 20)
        {
            this.doWorkAsync = doWorkAsync;
            this.shouldRetry = shouldRetry;
            this.backoff = backoff;
            this.thingsToRetry = retryStorage;
            this.name = name;
            this.maxRetries = maxRetries;
        }

        /// <summary>
        /// Calls the wrapped function using the given parameters.
        /// If the work fails as determined by the shouldRety function,
        /// It queues the work for retry using the given backoff strategy.
        /// </summary>
        /// <param name="params">Parameters passed to the wrapped function.</param>
        /// <param name="cancellationToken">Cancels task.</param>
        /// <returns>Result of the task.</returns>
        public async Task<TResult> DoWorkAsync(TParam @params, CancellationToken cancellationToken)
        {
            CancellationToken ct = CancellationTokenSource.CreateLinkedTokenSource(this.cancellationTokenSource.Token, cancellationToken).Token;

            TResult result = await this.doWorkAsync(@params, ct);

            if (this.shouldRetry(result))
            {
                this.log.LogInformation($"{this.name} task failed. Adding to retry queue.");
                this.thingsToRetry.Add(Guid.NewGuid(), @params);
                this.SignalRetry();
            }

            return result;
        }

        /// <summary>
        /// Start retry process if not already retrying.
        /// </summary>
        void SignalRetry()
        {
            if (this.retryTask == null)
            {
                this.retryTask = Task.Run(async () =>
                {
                    await this.StartRetrying();
                    this.retryTask = null;
                });
            }
        }

        /// <summary>
        /// Retry periodically, specified by backoff.
        /// </summary>
        /// <returns>Task.</returns>
        async Task StartRetrying()
        {
            foreach (TimeSpan sleepTime in this.backoff.GetBackoff())
            {
                this.log.LogInformation($"{this.name} retry initiated. Delaying {sleepTime.Humanize()}");
                await Task.Delay(sleepTime, this.cancellationTokenSource.Token);

                if (await this.RetryAll())
                {
                    this.log.LogInformation($"All {this.name} retrys succeded");
                    return;
                }
            }

            this.log.LogError("Backoff strategy should never finish");
        }

        /// <summary>
        /// Do work again for all failed attempts.
        /// </summary>
        /// <returns>True if all work compleated succesfully.</returns>
        async Task<bool> RetryAll()
        {
            bool allCompleatedSuccesfully = true;
            foreach (var paramToRetry in this.thingsToRetry)
            {
                TResult result = await this.doWorkAsync(paramToRetry.Value, this.cancellationTokenSource.Token);
                if (this.numRetries[paramToRetry.Key]++ < this.maxRetries && this.shouldRetry(result))
                {
                    allCompleatedSuccesfully = false;
                }
                else
                {
                    this.thingsToRetry.Remove(paramToRetry.Key);
                }
            }

            return allCompleatedSuccesfully;
        }

        public void Dispose()
        {
            this.cancellationTokenSource.Cancel();
        }
    }
}
