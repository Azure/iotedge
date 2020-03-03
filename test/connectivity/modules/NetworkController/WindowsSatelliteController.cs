// Copyright (c) Microsoft. All rights reserved.
namespace NetworkController
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.NetworkController;

    class WindowsSatelliteController : INetworkController
    {
        string networkInterfaceName;

        public WindowsSatelliteController(string networkInterfaceName)
        {
            this.networkInterfaceName = networkInterfaceName;
        }

        public NetworkControllerType NetworkControllerType => throw new System.NotImplementedException();

        public Task<NetworkControllerStatus> GetNetworkControllerStatusAsync(CancellationToken cs)
        {
            throw new System.NotImplementedException();
        }

        public Task<bool> SetNetworkControllerStatusAsync(NetworkControllerStatus networkControllerStatus, CancellationToken cs)
        {
            throw new System.NotImplementedException();
        }
    }
}
