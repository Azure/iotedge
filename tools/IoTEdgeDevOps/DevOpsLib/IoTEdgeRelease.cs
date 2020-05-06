// Copyright (c) Microsoft. All rights reserved.
namespace DevOpsLib
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using DevOpsLib.VstsModels;

    public class IoTEdgeRelease
    {
        readonly int id;
        readonly ReleaseDefinitionId definitionId;
        readonly string name;
        readonly string sourceBranch;
        readonly VstsReleaseStatus status;
        readonly Uri webUri;
        readonly HashSet<IoTEdgeReleaseEnvironment> environments;

        public IoTEdgeRelease(
            int id,
            ReleaseDefinitionId definitionId,
            string name,
            string sourceBranch,
            VstsReleaseStatus status,
            Uri webUri,
            HashSet<IoTEdgeReleaseEnvironment> environments)
        {
            ValidationUtil.ThrowIfNonPositive(id, nameof(id));
            ValidationUtil.ThrowIfNullOrWhiteSpace(name, nameof(name));
            ValidationUtil.ThrowIfNullOrWhiteSpace(sourceBranch, nameof(sourceBranch));
            ValidationUtil.ThrowIfNull(webUri, nameof(webUri));
            ValidationUtil.ThrowIfNull(environments, nameof(environments));

            this.id = id;
            this.definitionId = definitionId;
            this.name = name;
            this.sourceBranch = sourceBranch;
            this.status = status;
            this.webUri = webUri;
            this.environments = environments;
        }

        public int Id => this.id;

        public ReleaseDefinitionId DefinitionId => this.definitionId;

        public string Name => this.name;

        public string SourceBranch => this.sourceBranch;

        public VstsReleaseStatus Status => this.status;

        public Uri WebUri => this.webUri;

        public int NumberOfEnvironments => this.environments.Count;

        public static IoTEdgeRelease Create(VstsRelease vstsRelease, string sourceBranch) =>
            new IoTEdgeRelease(
                vstsRelease.Id,
                vstsRelease.DefinitionId,
                vstsRelease.Name,
                sourceBranch,
                vstsRelease.Status,
                vstsRelease.WebUri,
                vstsRelease.Environments.Select(IoTEdgeReleaseEnvironment.Create).ToHashSet()
            );

        public bool HasResult() => this.id > 0;

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

            return this.Equals((IoTEdgeRelease)obj);
        }

        public override int GetHashCode() => this.id;

        public IoTEdgeReleaseEnvironment GetEnvironment(int environmentDefinitionId) =>
            this.environments.FirstOrDefault(e => e.DefinitionId == environmentDefinitionId) ??
            IoTEdgeReleaseEnvironment.CreateEnvironmentWithNoResult(environmentDefinitionId);

        protected bool Equals(IoTEdgeRelease other) =>
            this.id == other.id &&
            this.definitionId == other.definitionId &&
            string.Equals(this.name, other.name, StringComparison.Ordinal) &&
            string.Equals(this.sourceBranch, other.sourceBranch, StringComparison.Ordinal) &&
            this.webUri.Equals(other.webUri) &&
            this.environments.SetEquals(other.environments);
    }
}
