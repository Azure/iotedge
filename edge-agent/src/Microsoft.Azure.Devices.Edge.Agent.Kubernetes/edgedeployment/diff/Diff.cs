// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment.Diff
{
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Util;

    public class Diff<T>
    {
        public readonly IImmutableSet<T> Added;

        public readonly IImmutableSet<string> Removed;

        public readonly IImmutableSet<Update<T>> Updated;

        public Diff(IEnumerable<T> added, IEnumerable<string> removed, IEnumerable<Update<T>> updated)
        {
            this.Added = Preconditions.CheckNotNull(added, nameof(added)).ToImmutableHashSet();
            this.Removed = Preconditions.CheckNotNull(removed, nameof(removed)).ToImmutableHashSet();
            this.Updated = Preconditions.CheckNotNull(updated, nameof(updated)).ToImmutableHashSet();
        }

        public static Diff<T> Empty { get; } = new Diff<T>(ImmutableList<T>.Empty, ImmutableList<string>.Empty, ImmutableList<Update<T>>.Empty);

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

            return obj is Diff<T> diffObj && this.Equals(diffObj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = this.Updated.Aggregate(hash, (acc, item) => acc * 31 + item.GetHashCode());
                hash = this.Removed.Aggregate(hash, (acc, item) => acc * 31 + item.GetHashCode());
                hash = this.Added.Aggregate(hash, (acc, item) => acc * 31 + item.GetHashCode());
                return hash;
            }
        }

        protected bool Equals(Diff<T> other) =>
            this.Updated.SetEquals(other.Updated)
            && this.Added.SetEquals(other.Added)
            && this.Removed.SetEquals(other.Removed);

        public class Builder
        {
            IReadOnlyList<T> added;
            IReadOnlyList<Update<T>> updated;
            IReadOnlyList<string> removed;

            public Builder WithAdded(params T[] added)
            {
                this.added = added;
                return this;
            }

            public Builder WithUpdated(params Update<T>[] updated)
            {
                this.updated = updated;
                return this;
            }

            public Builder WithRemoved(params string[] removed)
            {
                this.removed = removed;
                return this;
            }

            public Diff<T> Build() =>
                new Diff<T>(
                    this.added ?? ImmutableList<T>.Empty,
                    this.removed ?? ImmutableList<string>.Empty,
                    this.updated ?? ImmutableList<Update<T>>.Empty);
        }
    }
}
