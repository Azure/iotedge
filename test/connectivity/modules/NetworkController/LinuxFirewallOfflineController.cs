// Copyright (c) Microsoft. All rights reserved.
namespace NetworkController
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using ModuleUtil.NetworkControllerResult;

    class LinuxFirewallOfflineController : INetworkController
    {
        static readonly ILogger Log = Logger.Factory.CreateLogger<LinuxFirewallOfflineController>();
        readonly string networkInterfaceName;
        readonly string iotHubHostname;

        public LinuxFirewallOfflineController(string networkInterfaceName, string iotHubHostname)
        {
            this.networkInterfaceName = networkInterfaceName;
            this.iotHubHostname = iotHubHostname;
        }

        public NetworkStatus NetworkStatus => NetworkStatus.Offline;

        public async Task<bool> GetEnabledAsync(CancellationToken cs)
        {
            try
            {
                string output = await CommandExecutor.Execute(
                    "tc",
                    $"qdisc show dev {this.networkInterfaceName}",
                    cs);

                // parse output to see if online or offline
                return !output.Contains("qdisc noqueue");
            }
            catch (Exception e)
            {
                Log.LogError(e, "Failed to get network status");
                return false;
            }
        }

        public Task<bool> SetEnabledAsync(bool enabled, CancellationToken cs) => enabled ? this.AddDropRule(cs) : this.RemoveDropRule(cs);

        async Task<bool> RemoveDropRule(CancellationToken cs)
        {
            try
            {
                // Delete the root rules cleans the chilldren rules
                await CommandExecutor.Execute(
                   "tc",
                   $"qdisc delete dev {this.networkInterfaceName} root",
                   cs);

                return true;
            }
            catch (Exception e)
            {
                Log.LogError(e, "Failed to set accept rule");
                return false;
            }
        }

        async Task<bool> AddDropRule(CancellationToken cs)
        {
            try
            {
                IPAddress[] iothubAddresses = await Dns.GetHostAddressesAsync(this.iotHubHostname);
                if (iothubAddresses.Length == 0)
                {
                    throw new CommandExecutionException("No IP found for iothub hostname");
                }

                foreach (var item in iothubAddresses)
                {
                    Log.LogInformation($"Found iotHub IP {item}");
                }

                // Adding rules to filter packages by iotHub IP
                // Details about how the rules work https://wiki.archlinux.org/index.php/Advanced_traffic_control
                await CommandExecutor.Execute(
                    "tc",
                    $"qdisc add dev {this.networkInterfaceName} root handle 1: prio priomap 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0",
                    cs);

                await CommandExecutor.Execute(
                    "tc",
                    $"qdisc add dev {this.networkInterfaceName} parent 1:2 handle 20: netem loss 100%",
                    cs);

                await CommandExecutor.Execute(
                   "tc",
                   $"filter add dev {this.networkInterfaceName} parent 1:0 protocol ip u32 match ip src {iothubAddresses[0]} flowid 1:2",
                   cs);

                return true;
            }
            catch (Exception e)
            {
                Log.LogError(e, "Failed to set drop rule");
                return false;
            }
        }
    }
}
