// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Edgelet
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;

    public class RuntimeInfoProvider<T> : IRuntimeInfoProvider
    {
        readonly IModuleManager moduleManager;

        public RuntimeInfoProvider(IModuleManager moduleManager)
        {
            this.moduleManager = moduleManager;
        }

        public async Task<IEnumerable<ModuleRuntimeInfo>> GetModules(CancellationToken token)
        {
            IEnumerable<ModuleRuntimeInfo> modulesRuntimeInfo = await this.moduleManager.GetModules<T>(token);
            return modulesRuntimeInfo;
        }

        public Task<SystemInfo> GetSystemInfo() => this.moduleManager.GetSystemInfoAsync();
    }
}
