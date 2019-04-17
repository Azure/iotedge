// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Util;

    public class Diff
    {
        public Diff(IList<IModule> added, IList<IModule> updated, IList<IModule> desiredStatusUpdated, IList<string> removed)
        {
            this.Updated = Preconditions.CheckNotNull(updated, nameof(updated)).ToImmutableHashSet();
            this.Removed = Preconditions.CheckNotNull(removed, nameof(removed)).ToImmutableHashSet();
            this.DesiredStatusUpdated = Preconditions.CheckNotNull(desiredStatusUpdated, nameof(desiredStatusUpdated)).ToImmutableHashSet();
            this.Added = Preconditions.CheckNotNull(added, nameof(added)).ToImmutableHashSet();
            this.AddedOrUpdated = added.Concat(updated).ToImmutableHashSet();
        }

        public static Diff Empty { get; } = new Diff(ImmutableList<IModule>.Empty, ImmutableList<IModule>.Empty, ImmutableList<IModule>.Empty, ImmutableList<string>.Empty);

        public bool IsEmpty => this.Updated.Count == 0
                               && this.Added.Count == 0
                               && this.DesiredStatusUpdated.Count == 0
                               && this.Removed.Count == 0;

        // Set of new modules that were added
        public IImmutableSet<IModule> Added { get; }

        // Set of modules that were updated i.e. some property / config other than the ModuleStatus was updated
        public IImmutableSet<IModule> Updated { get; }

        // Set of modules whose configuration stayed the same, but whose ModuleStatus was updated
        public IImmutableSet<IModule> DesiredStatusUpdated { get; }

        // Set of modules that were removed
        public IImmutableSet<string> Removed { get; }

        // Added + Updated modules
        public IImmutableSet<IModule> AddedOrUpdated { get; }

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

            return obj is Diff diffObj && this.Equals(diffObj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = this.Updated.Aggregate(hash, (acc, item) => acc * 31 + item.GetHashCode());
                hash = this.Removed.Aggregate(hash, (acc, item) => acc * 31 + item.GetHashCode());
                hash = this.Added.Aggregate(hash, (acc, item) => acc * 31 + item.GetHashCode());
                hash = this.DesiredStatusUpdated.Aggregate(hash, (acc, item) => acc * 31 + item.GetHashCode());
                return hash;
            }
        }

        protected bool Equals(Diff other)
        {
            return this.Updated.SetEquals(other.Updated)
                   && this.Added.SetEquals(other.Added)
                   && this.DesiredStatusUpdated.SetEquals(other.DesiredStatusUpdated)
                   && this.Removed.SetEquals(other.Removed);
        }

        public class Builder
        {
            IList<IModule> added;
            IList<IModule> updated;
            IList<IModule> desiredStatusUpdated;
            IList<string> removed;

            public Builder WithAdded(params IModule[] added)
            {
                this.added = added;
                return this;
            }

            public Builder WithUpdated(params IModule[] updated)
            {
                this.updated = updated;
                return this;
            }

            public Builder WithDesiredStatusUpdated(params IModule[] desiredStatusUpdated)
            {
                this.desiredStatusUpdated = desiredStatusUpdated;
                return this;
            }

            public Builder WithRemoved(params string[] removed)
            {
                this.removed = removed;
                return this;
            }

            public Diff Build()
            {
                return new Diff(
                    this.added ?? ImmutableList<IModule>.Empty,
                    this.updated ?? ImmutableList<IModule>.Empty,
                    this.desiredStatusUpdated ?? ImmutableList<IModule>.Empty,
                    this.removed ?? ImmutableList<string>.Empty);
            }
        }
    }
}
