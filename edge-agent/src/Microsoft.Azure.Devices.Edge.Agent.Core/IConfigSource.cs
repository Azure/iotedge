// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Configuration;

    public interface IConfigSource : IDisposable
    {
        Task<ModuleSet> GetModuleSetAsync();

        event EventHandler<Diff> ModuleSetChanged;
        event EventHandler<Exception> ModuleSetFailed;

        IConfiguration Configuration { get; }
    }
}
