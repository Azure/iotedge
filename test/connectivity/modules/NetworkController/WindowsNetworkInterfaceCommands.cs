// Copyright (c) Microsoft. All rights reserved.
namespace NetworkController
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    class WindowsNetworkInterfaceCommands : INetworkInterfaceCommands
    {
        public Task<bool> Disable(CancellationToken token) =>
            throw new NotImplementedException();

        public Task<bool> Enable(CancellationToken token) => throw new NotImplementedException();
    }
}
