// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.WebJobs.Extensions.EdgeHub
{
    using System;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides a wrapper for a non-generic <see cref="T:System.Threading.Tasks.Task" /> and calls into the pipeline
    /// to retry only the generic version of the <see cref="T:System.Threading.Tasks.Task" />.
    /// </summary>
    class AsyncExecution : AsyncExecution<bool>
    {
        static Task<bool> cachedBoolTask;

        public AsyncExecution(Func<Task> taskAction, ShouldRetry shouldRetry, Func<Exception, bool> isTransient, Action<int, Exception, TimeSpan> onRetrying, bool fastFirstRetry, CancellationToken cancellationToken)
            : base(() => StartAsGenericTask(taskAction), shouldRetry, isTransient, onRetrying, fastFirstRetry, cancellationToken)
        {
        }

        /// <summary>
        /// Wraps the non-generic <see cref="T:System.Threading.Tasks.Task" /> into a generic <see cref="T:System.Threading.Tasks.Task" />.
        /// </summary>
        /// <param name="taskAction">The task to wrap.</param>
        /// <returns>A <see cref="T:System.Threading.Tasks.Task" /> that wraps the non-generic <see cref="T:System.Threading.Tasks.Task" />.</returns>
        static Task<bool> StartAsGenericTask(Func<Task> taskAction)
        {
            Task task = taskAction();
            if (task == null)
            {
                throw new ArgumentException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} cannot be null",
                        new object[]
                        {
                            "taskAction"
                        }),
                    nameof(taskAction));
            }

            if (task.Status == TaskStatus.RanToCompletion)
            {
                return GetCachedTask();
            }

            if (task.Status == TaskStatus.Created)
            {
                throw new ArgumentException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} must be scheduled",
                        new object[]
                        {
                            "taskAction"
                        }),
                    nameof(taskAction));
            }

            var tcs = new TaskCompletionSource<bool>();
            task.ContinueWith(
                t =>
                {
                    if (t.IsFaulted)
                    {
                        if (t.Exception != null)
                            tcs.TrySetException(t.Exception.InnerExceptions);
                        return;
                    }

                    if (t.IsCanceled)
                    {
                        tcs.TrySetCanceled();
                        return;
                    }

                    tcs.TrySetResult(true);
                },
                TaskContinuationOptions.ExecuteSynchronously);
            return tcs.Task;
        }

        static Task<bool> GetCachedTask()
        {
            if (cachedBoolTask == null)
            {
                var taskCompletionSource = new TaskCompletionSource<bool>();
                taskCompletionSource.TrySetResult(true);
                cachedBoolTask = taskCompletionSource.Task;
            }

            return cachedBoolTask;
        }
    }
}
