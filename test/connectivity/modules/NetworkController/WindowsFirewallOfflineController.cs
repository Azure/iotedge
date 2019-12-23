// Copyright (c) Microsoft. All rights reserved.
namespace NetworkController
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.NetworkControllerResult;
    using Microsoft.Azure.Devices.Edge.Util;

    class WindowsFirewallOfflineController : INetworkController
    {
        readonly string networkInterfaceName;

        public WindowsFirewallOfflineController(string networkInterfaceName)
        {
            this.networkInterfaceName =
                Preconditions.CheckNonWhiteSpace(networkInterfaceName, nameof(networkInterfaceName));
        }

        public NetworkControllerType NetworkControllerType => throw new NotImplementedException();

        public Task<NetworkControllerStatus> GetNetworkControllerStatusAsync(CancellationToken cs)
        {
            throw new NotImplementedException();
        }

        public Task<bool> SetNetworkControllerStatusAsync(NetworkControllerStatus networkControllerStatus, CancellationToken cs)
        {
            throw new NotImplementedException();
        }
    }
}
