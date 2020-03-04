// Copyright (c) Microsoft. All rights reserved.
namespace DevOpsLib
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using DevOpsLib.VstsModels;

    public class IoTEdgeReleaseDeployment : IEquatable<IoTEdgeReleaseDeployment>
    {
        readonly int id;
        readonly int attempt;
        readonly VstsDeploymentStatus status;
        readonly DateTime lastModifiedOn;
        readonly HashSet<IoTEdgePipelineTask> tasks;

        public IoTEdgeReleaseDeployment(int id, int attempt, VstsDeploymentStatus status, DateTime lastModifiedOn, HashSet<IoTEdgePipelineTask> tasks)
        {
            ValidationUtil.ThrowIfNonPositive(id, nameof(id));
            ValidationUtil.ThrowIfNonPositive(attempt, nameof(attempt));
            ValidationUtil.ThrowIfNull(tasks, nameof(tasks));

            this.id = id;
            this.attempt = attempt;
            this.status = status;
            this.lastModifiedOn = lastModifiedOn;
            this.tasks = tasks;
        }

        public int Id => this.id;

        public int Attempt => this.attempt;

        public VstsDeploymentStatus Status => this.status;

        public DateTime LastModifiedOn => this.lastModifiedOn;

        public HashSet<IoTEdgePipelineTask> Tasks => this.tasks;

        public static IoTEdgeReleaseDeployment Create(VstsReleaseDeployment vstsReleaseDeployment)
            => new IoTEdgeReleaseDeployment(vstsReleaseDeployment.Id, vstsReleaseDeployment.Attempt, vstsReleaseDeployment.Status, vstsReleaseDeployment.LastModifiedOn, vstsReleaseDeployment.Tasks?.Select(IoTEdgePipelineTask.Create).ToHashSet() ?? new HashSet<IoTEdgePipelineTask>());

        public bool Equals(IoTEdgeReleaseDeployment other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return this.id == other.id &&
                this.attempt == other.attempt &&
                this.status == other.status &&
                this.lastModifiedOn == other.lastModifiedOn &&
                this.tasks.SetEquals(other.tasks);
        }

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

            return this.Equals((IoTEdgeReleaseDeployment)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = this.Id.GetHashCode();
                hashCode = (hashCode * 397) ^ this.Attempt.GetHashCode();
                return hashCode;
            }
        }
    }
}
