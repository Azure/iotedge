// Copyright (c) Microsoft. All rights reserved.
namespace NetworkController
{
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using ModuleUtil.NetworkControllerResult;

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

        public NetworkStatus NetworkStatus => NetworkStatus.Offline;

        public Task<bool> GetEnabledAsync(CancellationToken cs) => this.underlyingController.GetEnabledAsync(cs);

        public Task<bool> SetEnabledAsync(bool enabled, CancellationToken cs)
        {
            return this.underlyingController.SetEnabledAsync(enabled, cs);
        }
    }
}
