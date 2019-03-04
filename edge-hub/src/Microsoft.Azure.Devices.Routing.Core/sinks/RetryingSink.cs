// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Sinks
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using Microsoft.Azure.Devices.Routing.Core.Util;

    public class RetryingSink<T> : ISink<T>
    {
        readonly ISink<T> underlying;
        readonly RetryPolicy retryPolicy;

        public RetryingSink(ISink<T> underlying, RetryPolicy retryPolicy)
        {
            this.underlying = Preconditions.CheckNotNull(underlying, nameof(underlying));
            this.retryPolicy = Preconditions.CheckNotNull(retryPolicy, nameof(retryPolicy));
        }

        public Task<ISinkResult<T>> ProcessAsync(T t, CancellationToken token) =>
            this.ProcessAsync(new[] { t }, token);

        public async Task<ISinkResult<T>> ProcessAsync(ICollection<T> ts, CancellationToken token)
        {
            var succeeded = new List<T>();
            var failed = new List<T>();
            var invalid = new List<InvalidDetails<T>>();
            ICollection<T> messages = ts;
            ISinkResult<T> rv;

            ShouldRetry shouldRetry = this.retryPolicy.RetryStrategy.GetShouldRetry();
            try
            {
                int i = 0;
                SendFailureDetails failureDetails = null;
                bool finished = false;
                while (!finished)
                {
                    token.ThrowIfCancellationRequested();

                    ISinkResult<T> result = await this.underlying.ProcessAsync(messages, token);
                    succeeded.AddRange(result.Succeeded);
                    invalid.AddRange(result.InvalidDetailsList);

                    if (result.IsSuccessful)
                    {
                        failureDetails = null;
                        finished = true;
                    }
                    else
                    {
                        TimeSpan retryAfter;
                        failureDetails = result.SendFailureDetails.OrDefault();
                        if (failureDetails != null && this.retryPolicy.ErrorDetectionStrategy.IsTransient(failureDetails.RawException) && shouldRetry(i, failureDetails.RawException, out retryAfter))
                        {
                            // if we should retry, set the next group of messages to send
                            // to the failed messages from the previous attempt
                            messages = result.Failed;

                            Preconditions.CheckRange(retryAfter.TotalMilliseconds, 0.0);
                            await Task.Delay(retryAfter, token);
                            i++;
                        }
                        else
                        {
                            // do not retry, return failed messages as failed
                            failed.AddRange(result.Failed);
                            finished = true;
                        }
                    }
                }

                rv = new SinkResult<T>(succeeded, failed, invalid, failureDetails);
            }
            catch (OperationCanceledException ex)
            {
                failed.AddRange(messages);
                rv = new SinkResult<T>(succeeded, failed, invalid, new SendFailureDetails(FailureKind.InternalError, ex));
            }
            catch (Exception ex)
            {
                failed.AddRange(messages);
                rv = new SinkResult<T>(succeeded, failed, invalid, new SendFailureDetails(FailureKind.InternalError, ex));
            }

            return rv;
        }

        public Task CloseAsync(CancellationToken token) => this.underlying.CloseAsync(token);
    }
}
