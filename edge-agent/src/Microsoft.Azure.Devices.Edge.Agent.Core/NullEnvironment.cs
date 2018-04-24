// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System.Threading;
    using System.Threading.Tasks;

    public class NullEnvironment : IEnvironment
    {
        public static NullEnvironment Instance { get; } = new NullEnvironment();

        public string OperatingSystemType => string.Empty;

        public string Architecture => string.Empty;

        NullEnvironment()
        {
        }

        public Task<ModuleSet> GetModulesAsync(CancellationToken token) => Task.FromResult(ModuleSet.Empty);

        public Task<IEdgeAgentModule> GetEdgeAgentModuleAsync(CancellationToken token) => Task.FromResult((IEdgeAgentModule)null);

        public Task<IRuntimeInfo> GetRuntimeInfoAsync() => Task.FromResult(UnknownRuntimeInfo.Instance as IRuntimeInfo);
    }
}
