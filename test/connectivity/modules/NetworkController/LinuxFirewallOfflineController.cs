// Copyright (c) Microsoft. All rights reserved.
namespace NetworkController
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    class LinuxFirewallOfflineController : IController
    {
        static readonly ILogger Log = Logger.Factory.CreateLogger<LinuxFirewallOfflineController>();
        readonly string networkInterfaceName;

        public LinuxFirewallOfflineController(string networkInterfaceName)
        {
            this.networkInterfaceName = networkInterfaceName;
        }

        public string Description => "LinuxFirewallOffline";

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

        public Task<bool> SetStatus(NetworkStatus status, CancellationToken cs)
        {
            switch (status)
            {
                case NetworkStatus.Restricted:
                    return this.AddDropRule(cs);
                case NetworkStatus.Default:
                    return this.RemoveDropRule(cs);
                default:
                    throw new NotSupportedException($"Set status '{status}' is not supported.");
            }
        }

        async Task<bool> RemoveDropRule(CancellationToken cs)
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

        async Task<bool> AddDropRule(CancellationToken cs)
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
