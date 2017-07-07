// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Util;

    public class Diff
    {
        public static Diff Empty { get; } = new Diff(ImmutableList<IModule>.Empty, ImmutableList<string>.Empty);

        public bool IsEmpty => this.Updated.Count == 0 && this.Removed.Count == 0;

        /// <summary>
        /// List of modules that have been updated
        /// </summary>
        public IImmutableSet<IModule> Updated { get; }

        /// <summary>
        /// List of modules names that have been removed
        /// </summary>
        public IImmutableSet<string> Removed { get; }

		public Diff(IList<IModule> updated, IList<string> removed)
        {
            this.Updated = Preconditions.CheckNotNull(updated, nameof(updated)).ToImmutableHashSet();
            this.Removed = Preconditions.CheckNotNull(removed, nameof(removed)).ToImmutableHashSet();
        }

        public static Diff Create(params IModule[] updated) => new Diff(updated.ToList(), ImmutableList<string>.Empty);

        protected bool Equals(Diff other)
        {
            return this.Updated.SetEquals(other.Updated) && this.Removed.SetEquals(other.Removed);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            return obj is Diff && this.Equals((Diff)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = this.Updated.Aggregate(hash, (acc, item) => acc * 31 + item.GetHashCode());
                hash = this.Removed.Aggregate(hash, (acc, item) => acc * 31 + item.GetHashCode());
                return hash;
            }
        }
    }
}