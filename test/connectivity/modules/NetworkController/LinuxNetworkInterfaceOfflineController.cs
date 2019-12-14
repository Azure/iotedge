// Copyright (c) Microsoft. All rights reserved.
namespace NetworkController
{
    using System;
    using System.Net.NetworkInformation;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    class LinuxNetworkInterfaceOfflineController : IController
    {
        static readonly ILogger Log = Logger.Factory.CreateLogger<LinuxNetworkInterfaceOfflineController>();
        readonly string interfaceName;

        public LinuxNetworkInterfaceOfflineController(string interfaceName)
        {
            this.interfaceName = interfaceName;
        }

        public string Description => "LinuxNetworkInterfaceOffline";

        public Task<NetworkStatus> GetStatus(CancellationToken cs)
        {
            OperationalStatus status = this.GetNetworkInterfaceStatus();
            switch (status)
            {
                case OperationalStatus.Up:
                    return Task.FromResult(NetworkStatus.Default);
                case OperationalStatus.Unknown:
                    return Task.FromResult(NetworkStatus.Unknown);
                default:
                    return Task.FromResult(NetworkStatus.Restricted);
            }
        }

        public Task<bool> SetStatus(NetworkStatus status, CancellationToken cs)
        {
            switch (status)
            {
                case NetworkStatus.Restricted:
                    return this.Disable(cs);
                case NetworkStatus.Default:
                    return this.Enable(cs);
                default:
                    Log.LogDebug($"Status not set {status}");
                    throw new NotSupportedException($"Set status '{status}' is not supported.");
            }
        }

        async Task<bool> Disable(CancellationToken token)
        {
            try
            {
                string output = await CommandExecutor.Execute("ifconfig", $"{this.interfaceName} down", token);
                Log.LogInformation($"Disabled {this.interfaceName}");

                return true;
            }
            catch (Exception e)
            {
                Log.LogError(e, "Failed to disable");
                return false;
            }
        }

        async Task<bool> Enable(CancellationToken token)
        {
            try
            {
                var exitCode = await CommandExecutor.Execute("ifconfig", $"{this.interfaceName} up", token);
                Log.LogInformation($"Enabled {this.interfaceName}");
                return true;
            }
            catch (Exception e)
            {
                Log.LogError(e, "Failed to disable");
                return false;
            }
        }

        OperationalStatus GetNetworkInterfaceStatus()
        {
            foreach (NetworkInterface item in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (item.Name.Equals(this.interfaceName))
                {
                    return item.OperationalStatus;
                }
            }

            return OperationalStatus.Unknown;
        }
    }
}
