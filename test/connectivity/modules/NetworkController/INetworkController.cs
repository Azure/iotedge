// Copyright (c) Microsoft. All rights reserved.
namespace NetworkController
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.NetworkControllerResult;

    interface INetworkController
    {
        NetworkControllerType NetworkControllerType { get; }

        Task<bool> SetNetworkControllerStatusAsync(NetworkControllerStatus networkControllerStatus, CancellationToken cs);

        Task<NetworkControllerStatus> GetNetworkControllerStatusAsync(CancellationToken cs);
    }
}
