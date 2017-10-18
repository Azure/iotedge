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
        string OperatingSystemType { get; }

        string Architecture { get; }

        /// <summary>
        /// Returns a ModuleSet representing the current state of the system (current reality)
        /// </summary>
        /// <returns></returns>
        Task<ModuleSet> GetModulesAsync(CancellationToken token);

        Task<IModule> GetEdgeAgentModuleAsync(CancellationToken token);

        /// <summary>
        /// In the general case information captured in a <see cref="IRuntimeInfo"/> can
        /// potentially be sourced from different sources. When using IoT Hub and Docker
        /// for example, we find that some parts of the runtime information is extracted
        /// from the agent's desired properties in IoT Hub and other parts are derived
        /// from Docker. This method exists so that the environment is able to essentially
        /// embellish the runtime information with additional context derived from the
        /// environment.
        /// </summary>
        /// <param name="runtimeInfo">The existing <see cref="IRuntimeInfo"/> to be
        /// augmented with additional information. This parameter is allowed to be
        /// <c>null</c> in which case the environment provides just the information
        /// that it is able to.</param>
        /// <returns>An <see cref="IRuntimeInfo"/> object that may contain additional
        /// context. Note that it is completely valid for this method to simply
        /// return what is passed to it.</returns>
        Task<IRuntimeInfo> GetUpdatedRuntimeInfoAsync(IRuntimeInfo runtimeInfo);
    }
}
