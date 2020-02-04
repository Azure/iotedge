// Copyright (c) Microsoft. All rights reserved.
namespace NetworkController
{
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.NetworkController;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    class OfflineController : INetworkController
    {
        static readonly ILogger Log = Logger.Factory.CreateLogger<OfflineController>();
        readonly INetworkController underlyingController;

        public OfflineController(string networkInterfaceName, string iotHubHostname, NetworkProfileSetting settings)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                this.underlyingController = new WindowsFirewallOfflineController(networkInterfaceName);
            }
            else
            {
                this.underlyingController = new LinuxTrafficControlController(settings, networkInterfaceName, iotHubHostname);
            }
        }

        public NetworkControllerType NetworkControllerType => NetworkControllerType.Offline;

        public Task<NetworkControllerStatus> GetNetworkControllerStatusAsync(CancellationToken cs) => this.underlyingController.GetNetworkControllerStatusAsync(cs);

        public Task<bool> SetNetworkControllerStatusAsync(NetworkControllerStatus networkControllerStatus, CancellationToken cs)
        {
            return this.underlyingController.SetNetworkControllerStatusAsync(networkControllerStatus, cs);
        }
    }
}
