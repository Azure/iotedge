// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents the current state of the environment
    /// the agent is running in.
    /// </summary>
    public interface IEnvironment
    {
        /// <summary>
        /// Returns a ModuleSet representing the current state of the system (current reality)
        /// </summary>
        /// <returns></returns>
        Task<ModuleSet> GetModulesAsync(CancellationToken token);
    }
}