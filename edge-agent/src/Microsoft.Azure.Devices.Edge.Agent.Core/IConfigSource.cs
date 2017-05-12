// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System;
    using System.Threading.Tasks;

    public interface IConfigSource : IDisposable
    {
        Task<ModuleSet> GetConfigAsync();

        event EventHandler<Diff> Changed;
        event EventHandler<Exception> Failed;
    }
}
