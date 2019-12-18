// Copyright (c) Microsoft. All rights reserved.
namespace NetworkController
{
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    class FirewallOfflineController : IController
    {
        static readonly ILogger Log = Logger.Factory.CreateLogger<FirewallOfflineController>();
        readonly IController underlyingController;

        public FirewallOfflineController(string networkInterfaceName, string iothubHosname)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                this.underlyingController = new WindowsFirewallOfflineController(networkInterfaceName);
            }
            else
            {
                this.underlyingController = new LinuxFirewallOfflineController(networkInterfaceName, iothubHosname);
            }
        }

        public string Description => "FirewallOffline";

        public Task<NetworkStatus> GetStatus(CancellationToken cs) => this.underlyingController.GetStatus(cs);

        public async Task<bool> SetStatus(NetworkStatus status, CancellationToken cs)
        {
            bool result = await this.underlyingController.SetStatus(status, cs);

            string resultMessage = result ? "succeded" : "failed";
            Log.LogInformation($"Command SetStatus {status} execution {resultMessage}, network status {status}");

            NetworkStatus reportedStatus = await this.GetStatus(cs);
            return result && reportedStatus == status;
        }
    }
}
