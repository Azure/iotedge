// Copyright (c) Microsoft. All rights reserved.
namespace NetworkController
{
    using System.Threading;
    using System.Threading.Tasks;

    interface INetworkController
    {
        string Description { get; }

        Task<bool> SetStatus(NetworkStatus status, CancellationToken cs);

        Task<NetworkStatus> GetStatus(CancellationToken cs);
    }
}
