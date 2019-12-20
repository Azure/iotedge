// Copyright (c) Microsoft. All rights reserved.
namespace NetworkController
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using ModuleUtil.NetworkControllerResult;

    class WindowsFirewallOfflineController : INetworkController
    {
        readonly string networkInterfaceName;

        public WindowsFirewallOfflineController(string networkInterfaceName)
        {
            this.networkInterfaceName =
                Preconditions.CheckNonWhiteSpace(networkInterfaceName, nameof(networkInterfaceName));
        }

        public NetworkStatus NetworkStatus => throw new NotImplementedException();

        public Task<bool> GetEnabledAsync(CancellationToken cs)
        {
            throw new NotImplementedException();
        }

        public Task<bool> SetEnabledAsync(bool enabled, CancellationToken cs)
        {
            throw new NotImplementedException();
        }
    }
}
