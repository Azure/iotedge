// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System.Collections.Immutable;
    using System.Threading.Tasks;

    /// <summary>
    /// Allows the deployment strategy to be abstracted.
    /// </summary>
    public interface IPlanner
    {
        Task<Plan> PlanAsync(ModuleSet desired, ModuleSet current, IRuntimeInfo runtimeInfo, IImmutableDictionary<string, IModuleIdentity> moduleIdentities);

        Task<Plan> CreateShutdownPlanAsync(ModuleSet modules);
    }
}
