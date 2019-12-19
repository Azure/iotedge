// Copyright (c) Microsoft. All rights reserved.
namespace NetworkController
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    class WindowsFirewallOfflineController : INetworkController
    {
        readonly string networkInterfaceName;

        public WindowsFirewallOfflineController(string networkInterfaceName)
        {
            this.networkInterfaceName =
                Preconditions.CheckNonWhiteSpace(networkInterfaceName, nameof(networkInterfaceName));
        }

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
