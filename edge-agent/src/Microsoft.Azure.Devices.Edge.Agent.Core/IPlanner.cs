// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System.Threading.Tasks;

    using System.Collections.Immutable;

    /// <summary>
    /// Allows the deployment strategy to be abstracted.
    /// </summary>
    public interface IPlanner
    {
        Task<Plan> PlanAsync(ModuleSet desired, ModuleSet current, IImmutableDictionary<string, IModuleIdentity> moduleIdentities);
    }
}
