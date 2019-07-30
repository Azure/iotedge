// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using Microsoft.Azure.Devices.Edge.Util.Json;
    using Newtonsoft.Json;

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

        public override string ToString() => this.Exception.Match(
            e => $"Background task Status = {this.Status}, Operation: {this.Operation}, Exception: {e}",
            () => $"Background task Status: {this.Status}, Operation: {this.Operation}");
    }
}
