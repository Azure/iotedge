// Copyright (c) Microsoft. All rights reserved.
namespace NetworkController
{
    using System;
    using System.Net.NetworkInformation;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    class NetworkInterfaceOfflineController : IController
    {
        static readonly ILogger Log = Logger.Factory.CreateLogger<NetworkInterfaceOfflineController>();

        readonly INetworkInterfaceCommands networkCommands;
        readonly string networkInterfaceName;

        public NetworkInterfaceOfflineController(string dockerInterfaceName)
        {
            this.networkInterfaceName =
                Preconditions.CheckNonWhiteSpace(dockerInterfaceName, nameof(dockerInterfaceName));

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                this.networkCommands = new WindowsNetworkInterfaceCommands();
            }
            else
            {
                this.networkCommands = new LinuxNetworkInterfaceCommands(this.networkInterfaceName);
            }
        }

        public string Description => "NetworkInterfaceOffline";

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

        public async Task<bool> AddNetworkControllingRule(CancellationToken cs)
        {
            bool result = await this.networkCommands.Disable(cs);
            NetworkStatus status = await this.GetStatus(cs);
            Log.LogInformation($"Command AddNetworkControllingRule success {result}, network status {status}");

            return result && status == NetworkStatus.Restricted;
        }

        public async Task<bool> RemoveNetworkControllingRule(CancellationToken cs)
        {
            bool result = await this.networkCommands.Enable(cs);
            NetworkStatus status = await this.GetStatus(cs);
            Log.LogInformation($"Command RemoveNetworkControllingRule execution success {result}, network status {status}");

            return result && status == NetworkStatus.Default;
        }

        public Task<bool> SetStatus(NetworkStatus status, CancellationToken cs)
        {
            switch (status)
            {
                case NetworkStatus.Restricted:
                    return this.AddNetworkControllingRule(cs);
                case NetworkStatus.Default:
                    return this.RemoveNetworkControllingRule(cs);
                default:
                    Log.LogDebug($"Status not set {status}");
                    throw new NotSupportedException($"Status '{status}' is not supported.");
            }
        }

        OperationalStatus GetNetworkInterfaceStatus()
        {
            foreach (NetworkInterface item in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (item.Name.Equals(this.networkInterfaceName))
                {
                    return item.OperationalStatus;
                }
            }

            return OperationalStatus.Unknown;
        }
    }
}
