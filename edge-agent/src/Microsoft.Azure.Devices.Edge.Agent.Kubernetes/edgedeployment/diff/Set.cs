// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment.Diff
{
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Util;

    public class Set<T>
    {
        readonly IReadOnlyDictionary<string, T> items;

        public Set(IReadOnlyDictionary<string, T> items)
        {
            this.items = Preconditions.CheckNotNull(items, nameof(items));
        }

        public static readonly Set<T> Empty = new Set<T>(new Dictionary<string, T>());

        public Diff<T> Diff(Set<T> other) => this.Diff(other, EqualityComparer<T>.Default);

        public Diff<T> Diff(Set<T> other, IEqualityComparer<T> comparer)
        {
            // build list of items that are available in "other" set but not available in "this" set;
            // this represents items that may need to be created
            IEnumerable<T> added = this.items.Keys
                .Except(other.items.Keys)
                .Select(key => this.items[key]);

            // build list of items that are available in "this" set but not available in "other" set;
            // this represents items that may need to be removed
            IEnumerable<string> removed = other.items.Keys
                .Except(this.items.Keys);

            // build list of items that are available in both sets;
            // this represents items that may be updated
            IEnumerable<Update<T>> updated = this.items.Keys
                .Intersect(other.items.Keys)
                .Where(key => !comparer.Equals(this.items[key], other.items[key]))
                .Select(key => new Update<T>(other.items[key], this.items[key]));

            return new Diff<T>(added.ToImmutableList(), removed.ToImmutableList(), updated.ToImmutableList());
        }
    }
}
