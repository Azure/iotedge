// Copyright (c) Microsoft. All rights reserved.
namespace NetworkController
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    class WindowsNetworkInterfaceOfflineController : IController
    {
        public string Description => throw new NotImplementedException();

        public Task<NetworkStatus> GetStatus(CancellationToken cs)
        {
            throw new NotImplementedException();
        }

        public Task<bool> SetStatus(NetworkStatus status, CancellationToken cs)
        {
            throw new NotImplementedException();
        }
    }
}
