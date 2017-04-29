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
        public IImmutableList<IModule> Updated { get; }

        /// <summary>
        /// List of modules names that have been removed
        /// </summary>
        public IImmutableList<string> Removed { get; }

        public Diff(IList<IModule> updated, IList<string> removed)
        {
            this.Updated = Preconditions.CheckNotNull(updated, nameof(updated)).ToImmutableList();
            this.Removed = Preconditions.CheckNotNull(removed, nameof(removed)).ToImmutableList();
        }

        public static Diff Create(params IModule[] updated) => new Diff(updated.ToList(), ImmutableList<string>.Empty);
    }
}