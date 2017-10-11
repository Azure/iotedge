// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface IModuleIdentityLifecycleManager
    {
        Task<IEnumerable<IModuleIdentity>> UpdateModulesIdentity(ModuleSet desired, ModuleSet current);
    }
}
