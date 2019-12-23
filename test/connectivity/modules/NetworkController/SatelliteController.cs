// Copyright (c) Microsoft. All rights reserved.
namespace NetworkController
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.NetworkControllerResult;

    // TODO: implement satellite
    class SatelliteController : INetworkController
    {
        string name;

        public SatelliteController(string name)
        {
            this.name = name;
        }

        public NetworkControllerType NetworkControllerType => NetworkControllerType.Sattelite;

        public Task<bool> SetNetworkStatusAsync(NetworkStatus networkStatus, CancellationToken cs)
        {
            return Task.FromResult(true);
        }

        public Task<NetworkStatus> GetNetworkStatusAsync(CancellationToken cs)
        {
            return Task.FromResult(NetworkStatus.Disabled);
        }
    }
}
