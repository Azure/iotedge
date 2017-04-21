// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    public interface ICommand : IShowable
    {
        Task ExecuteAsync(CancellationToken token);

        Task UndoAsync(CancellationToken token);
    }
}