// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IProtocolHead : IDisposable
    {
        string Name { get; }

        Task CloseAsync(CancellationToken token);

        Task StartAsync();
    }
}
