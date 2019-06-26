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
            var correlationId = Guid.NewGuid().ToString();
            BackgroundTaskStatus backgroundTaskStatus = new BackgroundTaskStatus(BackgroundTaskRunStatus.Running, operation);
            TaskStatuses.TryAdd(correlationId, backgroundTaskStatus);
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
                        TaskStatuses.TryUpdate(correlationId, newStatus, backgroundTaskStatus);
                    }, cancellationToken),
                cancellationToken);
            return (correlationId, backgroundTaskStatus);
        }

        public static BackgroundTaskStatus GetStatus(string correlationId) =>
            TaskStatuses.TryGetValue(correlationId, out BackgroundTaskStatus status)
            ? status
            : new BackgroundTaskStatus(BackgroundTaskRunStatus.Unknown, string.Empty);
    }
}
