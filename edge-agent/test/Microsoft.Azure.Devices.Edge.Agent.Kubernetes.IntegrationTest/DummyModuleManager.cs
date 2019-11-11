// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.IntegrationTest
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Metrics;
    using Microsoft.Azure.Devices.Edge.Agent.Edgelet;
    using Microsoft.Azure.Devices.Edge.Agent.Edgelet.Models;
    using Microsoft.Azure.Devices.Edge.Util;

    public class DummyModuleManager : IModuleManager
    {
        public Task CreateModuleAsync(ModuleSpec moduleSpec) => throw new NotImplementedException();

        public Task StartModuleAsync(string name) => throw new NotImplementedException();

        public Task StopModuleAsync(string name) => throw new NotImplementedException();

        public Task DeleteModuleAsync(string name) => throw new NotImplementedException();

        public Task RestartModuleAsync(string name) => throw new NotImplementedException();

        public Task UpdateModuleAsync(ModuleSpec moduleSpec) => throw new NotImplementedException();

        public Task UpdateAndStartModuleAsync(ModuleSpec moduleSpec) => throw new NotImplementedException();

        public Task<SystemInfo> GetSystemInfoAsync(CancellationToken token) => Task.FromResult(new SystemInfo("kubernetes", "amd64", "v1"));

        public Task<SystemResources> GetSystemResourcesAsync() => throw new NotImplementedException();

        public Task<IEnumerable<ModuleRuntimeInfo>> GetModules<T>(CancellationToken token) => throw new NotImplementedException();

        public Task PrepareUpdateAsync(ModuleSpec moduleSpec) => throw new NotImplementedException();

        public Task<Stream> GetModuleLogs(string name, bool follow, Option<int> tail, Option<int> since, CancellationToken cancellationToken) => throw new NotImplementedException();
    }
}
