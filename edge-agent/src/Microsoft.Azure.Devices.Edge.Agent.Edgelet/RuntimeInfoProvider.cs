// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Edgelet
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;

    public class RuntimeInfoProvider<T> : IRuntimeInfoProvider
    {
        readonly IModuleManager moduleManager;

        public RuntimeInfoProvider(IModuleManager moduleManager)
        {
            this.moduleManager = Preconditions.CheckNotNull(moduleManager, nameof(moduleManager));
        }

        public Task<IEnumerable<ModuleRuntimeInfo>> GetModules(CancellationToken token) =>
            this.moduleManager.GetModules<T>(token);

        public Task<Stream> GetModuleLogs(string module, bool follow, Option<int> tail, Option<string> since, Option<string> until, CancellationToken cancellationToken) =>
             this.moduleManager.GetModuleLogs(module, follow, tail, since, until, cancellationToken);

        public Task<SystemInfo> GetSystemInfo(CancellationToken token) => this.moduleManager.GetSystemInfoAsync(token);

        public Task<Stream> GetSupportBundle(Option<string> since, Option<string> until, Option<string> iothubHostname, Option<bool> edgeRuntimeOnly, CancellationToken token) =>
            this.moduleManager.GetSupportBundle(since, until, iothubHostname, edgeRuntimeOnly, token);
    }
}
