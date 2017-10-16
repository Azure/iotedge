// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System.Collections.Immutable;
    using System.Threading.Tasks;

    public interface IModuleIdentityLifecycleManager
    {
        Task<IImmutableDictionary<string, IModuleIdentity>> GetModuleIdentities(ModuleSet desired, ModuleSet current);
    }
}
