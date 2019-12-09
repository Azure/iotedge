// Copyright (c) Microsoft. All rights reserved.
namespace DevOpsLib
{
    using System;
    using DevOpsLib.VstsModels;

    public class IoTEdgeReleaseEnvironment : IEquatable<IoTEdgeReleaseEnvironment>
    {
        readonly int id;
        readonly int definitionId;
        readonly VstsEnvironmentStatus status;

        public IoTEdgeReleaseEnvironment(int id, int definitionId, VstsEnvironmentStatus status)
        {
            ValidationUtil.ThrowIfNegative(id, nameof(id));
            ValidationUtil.ThrowIfNonPositive(definitionId, nameof(definitionId));

            this.id = id;
            this.definitionId = definitionId;
            this.status = status;
        }

        public int Id => this.id;
        public int DefinitionId => this.definitionId;

        public VstsEnvironmentStatus Status => this.status;

        public static IoTEdgeReleaseEnvironment CreateEnvironmentWithNoResult(int definitionId)
            => new IoTEdgeReleaseEnvironment(0, definitionId, VstsEnvironmentStatus.Undefined);

        public static IoTEdgeReleaseEnvironment Create(VstsReleaseEnvironment vstsReleaseEnvironment)
            => new IoTEdgeReleaseEnvironment(vstsReleaseEnvironment.Id, vstsReleaseEnvironment.DefinitionId, vstsReleaseEnvironment.Status);

        public bool Equals(IoTEdgeReleaseEnvironment other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return this.id == other.id && this.definitionId == other.definitionId && this.status == other.status;
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

            return this.Equals((IoTEdgeReleaseEnvironment)obj);
        }

        public override int GetHashCode() => this.id;
    }
}
