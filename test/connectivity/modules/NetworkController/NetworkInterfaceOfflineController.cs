// Copyright (c) Microsoft. All rights reserved.
namespace NetworkController
{
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    class NetworkInterfaceOfflineController : INetworkController
    {
        static readonly ILogger Log = Logger.Factory.CreateLogger<NetworkInterfaceOfflineController>();

        readonly INetworkController underlyingConroller;
        readonly string networkInterfaceName;

        public NetworkInterfaceOfflineController(string networkInterfaceName)
        {
            this.networkInterfaceName =
                Preconditions.CheckNonWhiteSpace(networkInterfaceName, nameof(networkInterfaceName));

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                this.underlyingConroller = new WindowsNetworkInterfaceOfflineController();
            }
            else
            {
                this.underlyingConroller = new LinuxNetworkInterfaceOfflineController(this.networkInterfaceName);
            }
        }

        public string Description => "NetworkInterfaceOffline";

        public Task<NetworkStatus> GetStatus(CancellationToken cs)
        {
            return this.underlyingConroller.GetStatus(cs);
        }

        public Task<bool> SetStatus(NetworkStatus status, CancellationToken cs)
        {
            return this.underlyingConroller.SetStatus(status, cs);
        }
    }
}
