// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Edgelet
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Edgelet.GeneratedCode;

    public interface IModuleManager
    {
        Task CreateModuleAsync(ModuleSpec moduleSpec);

        Task StartModuleAsync(string name);

        Task StopModuleAsync(string name);

        Task DeleteModuleAsync(string name);

        Task RestartModuleAsync(string name);

        Task UpdateModuleAsync(ModuleSpec moduleSpec);

        Task UpdateAndStartModuleAsync(ModuleSpec moduleSpec);

        Task<SystemInfo> GetSystemInfoAsync();

        Task<IEnumerable<ModuleDetails>> GetModules(CancellationToken token);
    }
}
