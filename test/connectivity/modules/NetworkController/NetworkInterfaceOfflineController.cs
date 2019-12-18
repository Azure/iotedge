// Copyright (c) Microsoft. All rights reserved.
namespace NetworkController
{
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    class NetworkInterfaceOfflineController : IController
    {
        static readonly ILogger Log = Logger.Factory.CreateLogger<NetworkInterfaceOfflineController>();

        readonly IController underlyingConroller;
        readonly string networkInterfaceName;

        public NetworkInterfaceOfflineController(string dockerInterfaceName)
        {
            this.networkInterfaceName =
                Preconditions.CheckNonWhiteSpace(dockerInterfaceName, nameof(dockerInterfaceName));

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

        public async Task<bool> SetStatus(NetworkStatus status, CancellationToken cs)
        {
            bool result = await this.underlyingConroller.SetStatus(status, cs);
            NetworkStatus reportedStatus = await this.GetStatus(cs);
            string resultMessage = result ? "succeded" : "failed";
            Log.LogInformation($"Command SetStatus {status} {resultMessage}, network status {reportedStatus}");

            return result && reportedStatus == status;
        }
    }
}
