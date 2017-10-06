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
        /// List of modules that have been updated or added.
        /// </summary>
        /// <remarks>
        /// You might wonder why we do not have a separate property here to track "added" modules.
        /// The reason is that this type (<see cref="Diff"/>) is used to deserialize patch updates to
        /// the MMA's desired properties. The twin document delivered to us by IoT Hub does not
        /// distinguish between added and updated modules. What has been "added" or "updated"
        /// is only relevant when taking local state maintained in the MMA into account (capture
        /// via <see cref="ModuleSet.Diff(ModuleSet)"/>).
        /// </remarks>
        public IImmutableSet<IModule> Updated { get; }

        /// <summary>
        /// List of modules names that have been removed.
        /// </summary>
        /// <remarks>
        /// You might wonder why this is not an <see cref="IImmutableSet{IModule}"/> instead
        /// of what it is here. The reason is that this type (<see cref="Diff"/>) is used to
        /// deserialize patch updates to the MMA's desired properties. When a module is
        /// removed it shows up as an entry in the JSON where the key is the module name and
        /// the value is <c>null</c>. In this case, when deserializing the JSON, the deserializer
        /// (<see cref="Microsoft.Azure.Devices.Edge.Agent.Core.Serde.DiffSerde"/>) is unable
        /// to construct an <see cref="IModule"/> object from the value <c>null</c>. All it
        /// can do is populate a set of strings with module names. Hence an <see cref="IImmutableSet{string}"/>.
        /// </remarks>
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