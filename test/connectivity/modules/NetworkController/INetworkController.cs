// Copyright (c) Microsoft. All rights reserved.
namespace NetworkController
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.NetworkControllerResult;

    interface INetworkController
    {
        NetworkControllerType NetworkControllerType { get; }

        Task<bool> SetNetworkStatusAsync(NetworkStatus networkStatus, CancellationToken cs);

        Task<NetworkStatus> GetNetworkStatusAsync(CancellationToken cs);
    }
}
