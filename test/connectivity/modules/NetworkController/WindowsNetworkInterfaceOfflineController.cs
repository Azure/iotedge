// Copyright (c) Microsoft. All rights reserved.
namespace NetworkController
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    class WindowsNetworkInterfaceOfflineController : INetworkController
    {
        public string Description => throw new NotImplementedException();

        public Task<NetworkStatus> GetStatusAsync(CancellationToken cs)
        {
            throw new NotImplementedException();
        }

        public Task<bool> SetStatusAsync(NetworkStatus status, CancellationToken cs)
        {
            throw new NotImplementedException();
        }
    }
}
