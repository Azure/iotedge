// Copyright (c) Microsoft. All rights reserved.
namespace DevOpsLib
{
    using System;
    using DevOpsLib.VstsModels;

    public class IoTEdgePipelineTask : IEquatable<IoTEdgePipelineTask>
    {
        readonly int id;
        readonly string name;
        readonly string status;
        readonly DateTime startTime;
        readonly DateTime finishTime;
        readonly Uri logUrl;

        public IoTEdgePipelineTask(
            int id,
            string name,
            string status,
            DateTime startTime,
            DateTime finishTime,
            Uri logUrl)
        {
            ValidationUtil.ThrowIfNegative(id, nameof(id));
            ValidationUtil.ThrowIfNullOrWhiteSpace(name, nameof(name));
            ValidationUtil.ThrowIfNullOrWhiteSpace(status, nameof(status));

            this.id = id;
            this.name = name;
            this.status = status;
            this.startTime = startTime;
            this.finishTime = finishTime;
            this.logUrl = logUrl;
        }

        public int Id => this.id;

        public string Name => this.name;

        public string Status => this.status;

        public DateTime StartTime => this.startTime;

        public DateTime FinishTime => this.finishTime;

        public Uri LogUrl => this.logUrl;

        public static IoTEdgePipelineTask Create(VstsPipelineTask vstsRelease) =>
            new IoTEdgePipelineTask(
                vstsRelease.Id,
                vstsRelease.Name,
                vstsRelease.Status,
                vstsRelease.StartTime,
                vstsRelease.FinishTime,
                vstsRelease.LogUrl);

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != this.GetType())
            {
                return false;
            }

            return this.Equals((IoTEdgePipelineTask)obj);
        }

        public override int GetHashCode() => this.id;

        public bool Equals(IoTEdgePipelineTask other) =>
            this.id == other.id &&
            string.Equals(this.name, other.name, StringComparison.Ordinal) &&
            string.Equals(this.status, other.status, StringComparison.Ordinal) &&
            this.startTime == other.startTime &&
            this.finishTime == other.finishTime &&
            ((this.logUrl == null && other.logUrl == null) || (this.logUrl != null && this.logUrl.Equals(other.logUrl)));
    }
}
