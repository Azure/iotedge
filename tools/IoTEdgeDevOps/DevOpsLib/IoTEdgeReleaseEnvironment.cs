// Copyright (c) Microsoft. All rights reserved.
namespace DevOpsLib
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using DevOpsLib.VstsModels;

    public class IoTEdgeReleaseEnvironment : IEquatable<IoTEdgeReleaseEnvironment>
    {
        readonly int id;
        readonly int definitionId;
        readonly string definitionName;
        readonly VstsEnvironmentStatus status;
        readonly HashSet<IoTEdgeReleaseDeployment> deployments;

        public IoTEdgeReleaseEnvironment(int id, int definitionId, string definitionName, VstsEnvironmentStatus status, HashSet<IoTEdgeReleaseDeployment> deployments)
        {
            ValidationUtil.ThrowIfNegative(id, nameof(id));
            ValidationUtil.ThrowIfNonPositive(definitionId, nameof(definitionId));
            ValidationUtil.ThrowIfNull(definitionName, nameof(definitionName));
            ValidationUtil.ThrowIfNull(deployments, nameof(deployments));

            this.id = id;
            this.definitionId = definitionId;
            this.definitionName = definitionName;
            this.status = status;
            this.deployments = deployments;
        }

        public int Id => this.id;

        public int DefinitionId => this.definitionId;

        public string DefinitionName => this.definitionName;

        public VstsEnvironmentStatus Status => this.status;

        public HashSet<IoTEdgeReleaseDeployment> Deployments => this.deployments;

        public static IoTEdgeReleaseEnvironment CreateEnvironmentWithNoResult(int definitionId)
            => new IoTEdgeReleaseEnvironment(0, definitionId, string.Empty, VstsEnvironmentStatus.Undefined, new HashSet<IoTEdgeReleaseDeployment>());

        public static IoTEdgeReleaseEnvironment Create(VstsReleaseEnvironment vstsReleaseEnvironment)
            => new IoTEdgeReleaseEnvironment(
                vstsReleaseEnvironment.Id,
                vstsReleaseEnvironment.DefinitionId,
                vstsReleaseEnvironment.DefinitionName,
                vstsReleaseEnvironment.Status,
                vstsReleaseEnvironment?.Deployments?.Select(IoTEdgeReleaseDeployment.Create).ToHashSet() ?? new HashSet<IoTEdgeReleaseDeployment>());

        public bool HasResult()
        {
            return this.id > 0;
        }

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

            return this.id == other.id && this.definitionId == other.definitionId && this.status == other.status && this.deployments.SetEquals(other.deployments);
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
