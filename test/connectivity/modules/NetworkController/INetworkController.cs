// Copyright (c) Microsoft. All rights reserved.
namespace NetworkController
{
    using System.Threading;
    using System.Threading.Tasks;

    interface INetworkController
    {
        string Description { get; }

        Task<bool> SetStatusAsync(NetworkStatus status, CancellationToken cs);

        Task<NetworkStatus> GetStatusAsync(CancellationToken cs);
    }
}
