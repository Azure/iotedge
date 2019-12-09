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
        readonly int definitionId;
        readonly string name;
        readonly Uri webUri;
        readonly HashSet<IoTEdgeReleaseEnvironment> environments;

        public IoTEdgeRelease(
            int id,
            int definitionId,
            string name,
            Uri webUri,
            HashSet<IoTEdgeReleaseEnvironment> environments)
        {
            ValidationUtil.ThrowIfNonPositive(id, nameof(id));
            ValidationUtil.ThrowIfNonPositive(definitionId, nameof(definitionId));
            ValidationUtil.ThrowIfNullOrWhiteSpace(name, nameof(name));
            ValidationUtil.ThrowIfNull(webUri, nameof(webUri));
            ValidationUtil.ThrowIfNull(environments, nameof(environments));

            this.id = id;
            this.definitionId = definitionId;
            this.name = name;
            this.webUri = webUri;
            this.environments = environments;
        }

        public int Id => this.id;

        public int DefinitionId => this.definitionId;

        public string Name => this.name;

        public Uri WebUri => this.webUri;

        public int NumberOfEnvironments => this.environments.Count;

        public static IoTEdgeRelease Create(VstsRelease vstsRelease) =>
            new IoTEdgeRelease(
                vstsRelease.Id,
                vstsRelease.DefinitionId,
                vstsRelease.Name,
                vstsRelease.WebUri,
                vstsRelease.Environments.Select(IoTEdgeReleaseEnvironment.Create).ToHashSet()
            );

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
            this.webUri.Equals(other.webUri) &&
            this.environments.SetEquals(other.environments);
    }
}
