// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using Newtonsoft.Json;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Util;

    public class ModuleSet
    {
        public static ModuleSet Empty { get; } = new ModuleSet(ImmutableDictionary<string, IModule>.Empty);
        
        public IImmutableDictionary<string, IModule> Modules { get; }

        [JsonConstructor]
        public ModuleSet(IImmutableDictionary<string, IModule> modules)
        {
            this.Modules = Preconditions.CheckNotNull(modules, nameof(modules)).ToImmutableDictionary();
        }

        public static ModuleSet Create(params IModule[] modules) => new ModuleSet(modules.ToImmutableDictionary(m => m.Name, m => m));

        public bool TryGetModule(string key, out IModule module) => this.Modules.TryGetValue(key, out module);

        public ModuleSet ApplyDiff(Diff diff)
        {
            IImmutableDictionary<string, IModule> updated = this.Modules
                .SetItems(Preconditions.CheckNotNull(diff, nameof(diff)).Updated.Select(m => new KeyValuePair<string, IModule>(m.Name, m)))
                .RemoveRange(diff.Removed);
            return new ModuleSet(updated);
        }

        // TODO use equality comparer instead of equals?
        public Diff Diff(ModuleSet other)
        {
            IEnumerable<IModule> created = this.Modules.Keys
                .Except(other.Modules.Keys)
                .Select(key => this.Modules[key]);
            IEnumerable<string> removed = other.Modules.Keys
                .Except(this.Modules.Keys);
            IEnumerable<IModule> updated = this.Modules.Keys
                .Intersect(other.Modules.Keys)
                .Where(key => !this.Modules[key].Equals(other.Modules[key]))
                .Select(key => this.Modules[key]);
            return new Diff(created.Concat(updated).ToList(), removed.ToList());
        }
    }
}