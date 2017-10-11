// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface IServiceClient : IDisposable
    {
        Task<IEnumerable<Module>> GetModules();

        Task<Module> GetModule(string moduleId);

        Task<Module[]> CreateModules(IEnumerable<string> identities);

        Task<Module[]> UpdateModules(IEnumerable<Module> modules);

        Task RemoveModules(IEnumerable<string> identities);
    }
}
