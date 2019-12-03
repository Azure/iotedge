// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Edgelet
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Metrics;
    using Microsoft.Azure.Devices.Edge.Agent.Edgelet.Models;
    using Microsoft.Azure.Devices.Edge.Util;

    public interface IModuleManager
    {
        Task CreateModuleAsync(ModuleSpec moduleSpec);

        Task StartModuleAsync(string name);

        Task StopModuleAsync(string name);

        Task DeleteModuleAsync(string name);

        Task RestartModuleAsync(string name);

        Task UpdateModuleAsync(ModuleSpec moduleSpec);

        Task UpdateAndStartModuleAsync(ModuleSpec moduleSpec);

        Task<SystemInfo> GetSystemInfoAsync(CancellationToken token);

        Task<SystemResources> GetSystemResourcesAsync();

        Task<IEnumerable<ModuleRuntimeInfo>> GetModules<T>(CancellationToken token);

        Task PrepareUpdateAsync(ModuleSpec moduleSpec);

        Task<Stream> GetModuleLogs(string name, bool follow, Option<int> tail, Option<int> since, CancellationToken cancellationToken);
    }
}
