// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IProtocolHead : IDisposable
    {
        string Name { get; }

        Task StartAsync();

        Task CloseAsync(CancellationToken token);
    }
}
