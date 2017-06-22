// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using Microsoft.Azure.Devices.Edge.Util;
    using System;
    using System.Threading.Tasks;

    public interface IConfigSource : IDisposable
    {
        Task<ModuleSet> GetModuleSetAsync();

        bool ContainsKey(string key);

        Option<T> GetValue<T>(string key);

        Option<object> GetValue(string key, Type type);

        event EventHandler<Diff> Changed;
        event EventHandler<Exception> Failed;
    }
}
