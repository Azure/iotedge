// Copyright (c) Microsoft. All rights reserved.
namespace NetworkController
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.NetworkController;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    class LinuxTrafficControlController : INetworkController
    {
        static readonly ILogger Log = Logger.Factory.CreateLogger<LinuxTrafficControlController>();
        readonly string networkInterfaceName;
        readonly string hubHostname;
        readonly NetworkProfileSetting profileRuleSettings;

        public LinuxTrafficControlController(NetworkProfileSetting settings, string networkInterfaceName, string hubHostname)
        {
            this.networkInterfaceName = networkInterfaceName;
            this.hubHostname = hubHostname;
            this.profileRuleSettings = settings;
        }

        public NetworkControllerType NetworkControllerType => NetworkControllerType.Offline;

        public async Task<NetworkControllerStatus> GetNetworkControllerStatusAsync(CancellationToken cs)
        {
            try
            {
                string output = await CommandExecutor.Execute(
                    LinuxTrafficControllerHelper.CommandName,
                    LinuxTrafficControllerHelper.GetShowRules(this.networkInterfaceName),
                    cs);

                // parse output to see if online or offline
                if (output.Contains("qdisc noqueue"))
                {
                    Log.LogDebug("No rule is set");
                    return NetworkControllerStatus.Disabled;
                }
                else
                {
                    Log.LogDebug($"Found rules {output}");
                    return NetworkControllerStatus.Enabled;
                }
            }
            catch (Exception e)
            {
                Log.LogError(e, "Failed to get network status");
                return NetworkControllerStatus.Unknown;
            }
        }

        public Task<bool> SetNetworkControllerStatusAsync(NetworkControllerStatus status, CancellationToken cs)
        {
            switch (status)
            {
                case NetworkControllerStatus.Enabled:
                    return this.AddDropRule(cs);
                case NetworkControllerStatus.Disabled:
                    return this.RemoveDropRule(cs);
                default:
                    throw new NotSupportedException($"Set status '{status}' is not supported.");
            }
        }

        async Task<bool> RemoveDropRule(CancellationToken cs)
        {
            try
            {
                // Delete the root rules cleans the chilldren rules
                await CommandExecutor.Execute(
                    LinuxTrafficControllerHelper.CommandName,
                    LinuxTrafficControllerHelper.GetRemoveAllArguments(this.networkInterfaceName),
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
                IPAddress[] iothubAddresses = await Dns.GetHostAddressesAsync(this.hubHostname);
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
                Log.LogInformation($"Executing: {LinuxTrafficControllerHelper.GetRootRule(this.networkInterfaceName)}");

                await CommandExecutor.Execute(
                    LinuxTrafficControllerHelper.CommandName,
                    LinuxTrafficControllerHelper.GetRootRule(this.networkInterfaceName),
                    cs);

                Log.LogInformation($"Executing: {LinuxTrafficControllerHelper.GetNetworkEmulatorAddRule(this.networkInterfaceName, this.profileRuleSettings)}");

                await CommandExecutor.Execute(
                    LinuxTrafficControllerHelper.CommandName,
                    LinuxTrafficControllerHelper.GetNetworkEmulatorAddRule(this.networkInterfaceName, this.profileRuleSettings),
                    cs);

                Log.LogInformation($"Executing: {LinuxTrafficControllerHelper.GetIpFilter(this.networkInterfaceName, iothubAddresses)}");

                await CommandExecutor.Execute(
                    LinuxTrafficControllerHelper.CommandName,
                    LinuxTrafficControllerHelper.GetIpFilter(this.networkInterfaceName, iothubAddresses),
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
