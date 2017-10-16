// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System.Threading;
    using System.Threading.Tasks;

    public class NullEnvironment : IEnvironment
    {
        public static NullEnvironment Instance { get; } = new NullEnvironment();

        public string OperatingSystem => string.Empty;

        public string Architecture => string.Empty;

        NullEnvironment()
        {
        }

        public Task<ModuleSet> GetModulesAsync(CancellationToken token) => Task.FromResult(ModuleSet.Empty);

        public Task<IModule> GetEdgeAgentModuleAsync(CancellationToken token) => Task.FromResult((IModule)null);
    }
}
