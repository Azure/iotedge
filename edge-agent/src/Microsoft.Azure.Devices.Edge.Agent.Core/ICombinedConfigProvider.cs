// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    /// <summary>
    /// An implementation of this is expected to combine the type specific config info from the module and
    /// the runtime info objects and return it.
    /// For example, for Docker config, this implementation will combine docker image and docker create
    /// options from the module, and the registry credentials from the runtime info and return them.
    /// </summary>
    /// <typeparam name="T">The type of the combined config object returned</typeparam>
    public interface ICombinedConfigProvider<T>
    {
        T GetCombinedConfig(IModule module, IRuntimeInfo runtimeInfo);
    }
}
