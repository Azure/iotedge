// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util.Json;
    using Newtonsoft.Json;

    public enum BackgroundTaskRunStatus
    {
        NotStarted,
        Running,
        Completed,
        Cancelled,
        Failed,
        Unknown
    }

    public class BackgroundTaskStatus : IEquatable<BackgroundTaskStatus>
    {
        public BackgroundTaskStatus(BackgroundTaskRunStatus status, string operation)
            : this(status, operation, Option.None<Exception>())
        {
        }

        public BackgroundTaskStatus(BackgroundTaskRunStatus status, string operation, Option<Exception> exception)
        {
            this.Status = status;
            this.Operation = Preconditions.CheckNotNull(operation, nameof(operation));
            this.Exception = exception;
        }

        public BackgroundTaskRunStatus Status { get; }

        [JsonConverter(typeof(OptionConverter<Exception>))]
        public Option<Exception> Exception { get; }

        public string Operation { get; }

        public bool Equals(BackgroundTaskStatus other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return this.Status == other.Status
                   && this.Exception.Equals(other.Exception)
                   && this.Operation.Equals(other.Operation, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object obj) => this.Equals(obj as BackgroundTaskStatus);

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = (int)this.Status;
                hashCode = (hashCode * 397) ^ this.Exception.GetHashCode();
                hashCode = (hashCode * 397) ^ StringComparer.OrdinalIgnoreCase.GetHashCode(this.Operation);
                return hashCode;
            }
        }
    }

    public static class BackgroundTask
    {
        static readonly ConcurrentDictionary<string, BackgroundTaskStatus> TaskStatuses = new ConcurrentDictionary<string, BackgroundTaskStatus>();

        public static (string correlationId, BackgroundTaskStatus backgroundTaskStatus) Run(Func<Task> task, string operation, CancellationToken cancellationToken)
        {
            var tid = Guid.NewGuid().ToString();
            BackgroundTaskStatus backgroundTaskStatus = new BackgroundTaskStatus(BackgroundTaskRunStatus.Running, operation);
            TaskStatuses.TryAdd(tid, backgroundTaskStatus);
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
                        TaskStatuses.TryUpdate(tid, newStatus, backgroundTaskStatus);
                    }, cancellationToken),
                cancellationToken);
            return (tid, backgroundTaskStatus);
        }

        public static BackgroundTaskStatus GetStatus(string id) =>
            TaskStatuses.TryGetValue(id, out BackgroundTaskStatus status)
            ? status
            : new BackgroundTaskStatus(BackgroundTaskRunStatus.Unknown, string.Empty);
    }
}
