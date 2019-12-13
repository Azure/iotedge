// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util.Retrying
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Wraps an async function and attempts to retry the wrapped function if it fails
    /// as determined by a shouldRety function.
    /// </summary>
    /// <typeparam name="TParam">Parameter of the wrapped function. Use tupples if multiple inputs are desired.</typeparam>
    /// <typeparam name="TResult">Result of wrapped function.</typeparam>
    public class Retry<TParam, TResult>
    {
        readonly Func<TParam, CancellationToken, Task<TResult>> doAsyncWork;
        readonly Func<TResult, bool> shouldRetry;
        readonly IBackoff backoff;

        readonly List<TParam> thingsToRetry = new List<TParam>();
        readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        Task retryTask;

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

            TResult result = await this.doAsyncWork(@params, ct);

            if (this.shouldRetry(result))
            {
                this.thingsToRetry.Add(@params);
                this.SignalRetry();
            }

            return result;
        }

        /// <summary>
        /// Stary retry process if not already retrying.
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
                await Task.Delay(sleepTime, this.cancellationTokenSource.Token);

                if (await this.RetryAll())
                {
                    return;
                }
            }
        }

        /// <summary>
        /// Do work again for all failed attempts.
        /// </summary>
        /// <returns>True if all work compleated succesfully.</returns>
        async Task<bool> RetryAll()
        {
            bool allCompleatedSuccesfully = true;
            foreach (TParam @params in this.thingsToRetry)
            {
                TResult result = await this.doAsyncWork(@params, this.cancellationTokenSource.Token);
                if (this.shouldRetry(result))
                {
                    allCompleatedSuccesfully = false;
                }
                else
                {
                    this.thingsToRetry.Remove(@params);
                }
            }

            return allCompleatedSuccesfully;
        }
    }
}
