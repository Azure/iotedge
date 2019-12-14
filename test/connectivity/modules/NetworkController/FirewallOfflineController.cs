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
        readonly IController underlyingController;

        public FirewallOfflineController(string networkInterfaceName)
        {
            Preconditions.CheckNonWhiteSpace(networkInterfaceName, nameof(networkInterfaceName));

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                this.underlyingController = new WindowsFirewallOfflineController(networkInterfaceName);
            }
            else
            {
                this.underlyingController = new LinuxFirewallOfflineController(networkInterfaceName);
            }
        }

        public string Description => "FirewallOffline";

        public async Task<NetworkStatus> GetStatus(CancellationToken cs) => await this.underlyingController.GetStatus(cs);

        Task<bool> IController.SetStatus(NetworkStatus status, CancellationToken cs)
        {
            throw new NotImplementedException();
        }

        async Task<bool> SetStatus(NetworkStatus status, CancellationToken cs)
        {
            bool result = await this.underlyingController.SetStatus(status, cs);
            Log.LogInformation($"Command SetStatus {NetworkStatus.Restricted} execution success {result}, network status {status}");

            NetworkStatus reportedStatus = await this.GetStatus(cs);
            return result && reportedStatus == NetworkStatus.Restricted;
        }

        async Task<bool> RemoveNetworkControllingRule(CancellationToken cs)
        {
            bool result = await this.underlyingController.SetStatus(NetworkStatus.Default, cs);

            NetworkStatus status = await this.GetStatus(cs);
            Log.LogInformation($"Command RemoveDropRule execution success {result}, network status {status}");

            return result && status == NetworkStatus.Default;
        }
    }
}
