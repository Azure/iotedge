// Copyright (c) Microsoft. All rights reserved.
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Edge.ModuleUtil.NetworkController;

namespace NetworkController
{
    internal class WindowsCellularController : INetworkController
    {
        private string networkInterfaceName;

        public WindowsCellularController(string networkInterfaceName)
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
