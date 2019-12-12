// Copyright (c) Microsoft. All rights reserved.
namespace NetworkController
{
    using System;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    class FirewallOfflineController : IController
    {
        static readonly ILogger Log = Logger.Factory.CreateLogger<FirewallOfflineController>();
        readonly IFirewallCommands networkCommands;

        public FirewallOfflineController(string networkInterfaceName)
        {
            Preconditions.CheckNonWhiteSpace(networkInterfaceName, nameof(networkInterfaceName));

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                this.networkCommands = new WindowsFirewallCommands(networkInterfaceName);
            }
            else
            {
                this.networkCommands = new LinuxFirewallCommands(networkInterfaceName);
            }
        }

        public string Description => "FirewallOffline";

        public async Task<NetworkStatus> GetStatus(CancellationToken cs) => await this.networkCommands.GetStatus(cs);

        public Task<bool> SetStatus(NetworkStatus status, CancellationToken cs)
        {
            switch (status)
            {
                case NetworkStatus.Restricted:
                    return this.AddNetworkControllingRule(cs);
                case NetworkStatus.Default:
                    return this.RemoveNetworkControllingRule(cs);
                default:
                    Log.LogDebug($"Status not set {status}");
                    throw new NotSupportedException($"Status is not supported {status}");
            }
        }

        async Task<bool> AddNetworkControllingRule(CancellationToken cs)
        {
            bool result = await this.networkCommands.AddDropRule(cs);

            NetworkStatus status = await this.GetStatus(cs);
            Log.LogInformation($"Command AddDropRule execution success {result}, network status {status}");

            return result && status == NetworkStatus.Restricted;
        }

        async Task<bool> RemoveNetworkControllingRule(CancellationToken cs)
        {
            bool result = await this.networkCommands.RemoveDropRule(cs);

            NetworkStatus status = await this.GetStatus(cs);
            Log.LogInformation($"Command RemoveDropRule execution success {result}, network status {status}");

            return result && status == NetworkStatus.Default;
        }
    }
}
