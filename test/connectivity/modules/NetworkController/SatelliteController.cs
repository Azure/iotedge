// Copyright (c) Microsoft. All rights reserved.
namespace NetworkController
{
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.NetworkController;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    class SatelliteController : INetworkController
    {
        static readonly ILogger Log = Logger.Factory.CreateLogger<SatelliteController>();
        readonly INetworkController underlyingController;

        public SatelliteController(string networkInterfaceName, string hubHostname, NetworkProfileSetting settings)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                this.underlyingController = new WindowsSatelliteController(networkInterfaceName);
            }
            else
            {
                this.underlyingController = new LinuxTrafficControlController(settings, networkInterfaceName, hubHostname);
            }
        }

        public NetworkControllerType NetworkControllerType => NetworkControllerType.Satellite;

        public Task<bool> SetNetworkControllerStatusAsync(NetworkControllerStatus networkControllerStatus, CancellationToken cs) => this.underlyingController.SetNetworkControllerStatusAsync(networkControllerStatus, cs);

        public Task<NetworkControllerStatus> GetNetworkControllerStatusAsync(CancellationToken cs) => this.underlyingController.GetNetworkControllerStatusAsync(cs);
    }
}
