// Copyright (c) Microsoft. All rights reserved.
namespace NetworkController
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    class LinuxFirewallCommands : IFirewallCommands
    {
        static readonly ILogger Log = Logger.Factory.CreateLogger<LinuxFirewallCommands>();
        readonly string networkInterfaceName;

        public LinuxFirewallCommands(string networkInterfaceName)
        {
            this.networkInterfaceName = networkInterfaceName;
        }

        public async Task<NetworkStatus> GetStatus(CancellationToken cs)
        {
            try
            {
                string output = await CommandExecutor.Execute(
                    "tc",
                    $"qdisc show dev {this.networkInterfaceName}",
                    cs);

                // parse output to see if online or offline
                if (output.Contains("qdisc noqueue"))
                {
                    return NetworkStatus.Default;
                }
                else
                {
                    return NetworkStatus.Restricted;
                }
            }
            catch (Exception e)
            {
                Log.LogError(e, "Failed to get network status");
                return NetworkStatus.Unknown;
            }
        }

        public async Task<bool> RemoveDropRule(CancellationToken cs)
        {
            try
            {
                string output = await CommandExecutor.Execute(
                    "tc",
                    $"qdisc delete dev {this.networkInterfaceName} root netem loss 100%",
                    cs);

                return true;
            }
            catch (Exception e)
            {
                Log.LogError(e, "Failed to set accept rule");
                return false;
            }
        }

        public async Task<bool> AddDropRule(CancellationToken cs)
        {
            try
            {
                string output = await CommandExecutor.Execute(
                    "tc",
                    $"qdisc add dev {this.networkInterfaceName} root netem loss 100%",
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
