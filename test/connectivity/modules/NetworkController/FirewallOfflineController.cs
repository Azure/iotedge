// Copyright (c) Microsoft. All rights reserved.
namespace NetworkController
{
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    class FirewallOfflineController : INetworkController
    {
        static readonly ILogger Log = Logger.Factory.CreateLogger<FirewallOfflineController>();
        readonly INetworkController underlyingController;

        public FirewallOfflineController(string networkInterfaceName, string iotHubHostname)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                this.underlyingController = new WindowsFirewallOfflineController(networkInterfaceName);
            }
            else
            {
                this.underlyingController = new LinuxFirewallOfflineController(networkInterfaceName, iotHubHostname);
            }
        }

        public string Description => "FirewallOffline";

        public Task<NetworkStatus> GetStatus(CancellationToken cs) => this.underlyingController.GetStatus(cs);

        public Task<bool> SetStatus(NetworkStatus status, CancellationToken cs)
        {
            return this.underlyingController.SetStatus(status, cs);
        }
    }
}
