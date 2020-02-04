// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading;
    using System.Threading.Tasks;

    public static class BackgroundTask
    {
        static readonly ConcurrentDictionary<string, BackgroundTaskStatus> TaskStatuses = new ConcurrentDictionary<string, BackgroundTaskStatus>();

        public static (string correlationId, BackgroundTaskStatus backgroundTaskStatus) Run(Func<Task> task, string operation, CancellationToken cancellationToken)
        {
            BackgroundTaskStatus backgroundTaskStatus = new BackgroundTaskStatus(BackgroundTaskRunStatus.Running, operation);
            string correlationId = AddNewTask(backgroundTaskStatus);
            Task.Run(
                () => task().ContinueWith(
                    t =>
                    {
                        BackgroundTaskStatus GetNewStatus()
                        {
                            switch (t.Status)
                            {
                                case TaskStatus.Faulted:
                                    Exception exception = t.Exception is AggregateException aggregateException
                                        ? aggregateException.InnerException
                                        : t.Exception;
                                    return new BackgroundTaskStatus(BackgroundTaskRunStatus.Failed, operation, Option.Some(exception));

                                case TaskStatus.Canceled:
                                    return new BackgroundTaskStatus(BackgroundTaskRunStatus.Cancelled, operation);

                                case TaskStatus.RanToCompletion:
                                    return new BackgroundTaskStatus(BackgroundTaskRunStatus.Completed, operation);

                                default:
                                    return new BackgroundTaskStatus(BackgroundTaskRunStatus.Unknown, operation);
                            }
                        }

                        BackgroundTaskStatus newStatus = GetNewStatus();
                        if (!TaskStatuses.TryUpdate(correlationId, newStatus, backgroundTaskStatus))
                        {
                            // This should never happen.
                            BackgroundTaskStatus currentTask = GetStatus(correlationId);
                            throw new InvalidOperationException($"Failed to update background task status to - {newStatus}. Current task = {currentTask}");
                        }
                    }, cancellationToken),
                cancellationToken);
            return (correlationId, backgroundTaskStatus);
        }

        static string AddNewTask(BackgroundTaskStatus backgroundTaskStatus)
        {
            while (true)
            {
                var correlationId = Guid.NewGuid().ToString();
                if (TaskStatuses.TryAdd(correlationId, backgroundTaskStatus))
                {
                    return correlationId;
                }
            }
        }

        public static BackgroundTaskStatus GetStatus(string correlationId) =>
            TaskStatuses.TryGetValue(correlationId, out BackgroundTaskStatus status)
            ? status
            : new BackgroundTaskStatus(BackgroundTaskRunStatus.Unknown, string.Empty);
    }
}
