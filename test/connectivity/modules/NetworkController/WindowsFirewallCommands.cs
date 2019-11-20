// Copyright (c) Microsoft. All rights reserved.
namespace NetworkController
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    class WindowsFirewallCommands : IFirewallCommands
    {
        readonly string networkInterfaceName;

        public WindowsFirewallCommands(string networkInterfaceName)
        {
            this.networkInterfaceName =
                Preconditions.CheckNonWhiteSpace(networkInterfaceName, nameof(networkInterfaceName));
        }

        public Task<NetworkStatus> GetStatus(CancellationToken cs)
        {
            throw new NotImplementedException();
        }

        public Task<bool> UnsetDropRule(CancellationToken cs) => throw new NotImplementedException();

        public Task<bool> SetDropRule(CancellationToken cs) => throw new NotImplementedException();
    }
}
