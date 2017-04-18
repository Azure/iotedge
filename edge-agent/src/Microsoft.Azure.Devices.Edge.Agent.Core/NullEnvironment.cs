// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System.Threading;
    using System.Threading.Tasks;

    public class NullEnvironment : IEnvironment
    {
        public static NullEnvironment Instance { get; } = new NullEnvironment();

        NullEnvironment()
        {
        }

        public Task<ModuleSet> GetModulesAsync(CancellationToken token) => Task.FromResult(ModuleSet.Empty);
    }
}