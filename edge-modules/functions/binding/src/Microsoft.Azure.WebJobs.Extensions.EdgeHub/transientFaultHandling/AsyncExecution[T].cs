// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.EdgeHub
{
    /// <summary>
    /// Handles the execution and retries of the user-initiated task.
    /// </summary>
    /// <typeparam name="TResult">The result type of the user-initiated task.</typeparam>
    class AsyncExecution<TResult>
    {
        readonly Func<Task<TResult>> taskFunc;

        readonly ShouldRetry shouldRetry;

        readonly Func<Exception, bool> isTransient;

        readonly Action<int, Exception, TimeSpan> onRetrying;

        readonly bool fastFirstRetry;

        readonly CancellationToken cancellationToken;

        Task<TResult> previousTask;

        int retryCount;

        public AsyncExecution(Func<Task<TResult>> taskFunc, ShouldRetry shouldRetry, Func<Exception, bool> isTransient, Action<int, Exception, TimeSpan> onRetrying, bool fastFirstRetry, CancellationToken cancellationToken)
        {
            this.taskFunc = taskFunc;
            this.shouldRetry = shouldRetry;
            this.isTransient = isTransient;
            this.onRetrying = onRetrying;
            this.fastFirstRetry = fastFirstRetry;
            this.cancellationToken = cancellationToken;
        }

        internal Task<TResult> ExecuteAsync()
        {
            return this.ExecuteAsyncImpl(null);
        }

        Task<TResult> ExecuteAsyncImpl(Task ignore)
        {
            if (this.cancellationToken.IsCancellationRequested)
            {
                if (this.previousTask != null)
                {
                    return this.previousTask;
                }
                var taskCompletionSource = new TaskCompletionSource<TResult>();
                taskCompletionSource.TrySetCanceled();
                return taskCompletionSource.Task;
            }
            else
            {
                Task<TResult> task;
                try
                {
                    task = this.taskFunc();
                }
                catch (Exception ex)
                {
                    if (!this.isTransient(ex))
                    {
                        throw;
                    }
                    var taskCompletionSource2 = new TaskCompletionSource<TResult>();
                    taskCompletionSource2.TrySetException(ex);
                    task = taskCompletionSource2.Task;
                }
                if (task == null)
                {
                    throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, "{0} cannot be null", new object[]
                    {
                        "taskFunc"
                    }), nameof(this.taskFunc));
                }
                if (task.Status == TaskStatus.RanToCompletion)
                {
                    return task;
                }
                if (task.Status == TaskStatus.Created)
                {
                    throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, "{0} must be scheduled", new object[]
                    {
                        "taskFunc"
                    }), nameof(this.taskFunc));
                }
                return task.ContinueWith(new Func<Task<TResult>, Task<TResult>>(this.ExecuteAsyncContinueWith), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default).Unwrap();
            }
        }

        Task<TResult> ExecuteAsyncContinueWith(Task<TResult> runningTask)
        {
            if (!runningTask.IsFaulted || this.cancellationToken.IsCancellationRequested)
            {
                return runningTask;
            }
            TimeSpan zero;
            Exception innerException = runningTask.Exception.InnerException;
#pragma warning disable CS0618 // Type or member is obsolete
            if (innerException is RetryLimitExceededException)
#pragma warning restore CS0618 // Type or member is obsolete
            {
                var taskCompletionSource = new TaskCompletionSource<TResult>();
                if (innerException.InnerException != null)
                {
                    taskCompletionSource.TrySetException(innerException.InnerException);
                }
                else
                {
                    taskCompletionSource.TrySetCanceled();
                }
                return taskCompletionSource.Task;
            }
            if (!this.isTransient(innerException) || !this.shouldRetry(this.retryCount++, innerException, out zero))
            {
                return runningTask;
            }
            if (zero < TimeSpan.Zero)
            {
                zero = TimeSpan.Zero;
            }
            this.onRetrying(this.retryCount, innerException, zero);
            this.previousTask = runningTask;
            if (zero > TimeSpan.Zero && (this.retryCount > 1 || !this.fastFirstRetry))
            {
                return Task.Delay(zero, this.cancellationToken).ContinueWith(new Func<Task, Task<TResult>>(this.ExecuteAsyncImpl), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default).Unwrap();
            }
            return this.ExecuteAsyncImpl(null);
        }
    }
}
