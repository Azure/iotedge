// Copyright (c) Microsoft. All rights reserved.
namespace NetworkController
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    class LinuxNetworkInterfaceCommands : INetworkInterfaceCommands
    {
        static readonly ILogger Log = Logger.Factory.CreateLogger<LinuxNetworkInterfaceCommands>();
        readonly string interfaceName;

        public LinuxNetworkInterfaceCommands(string interfaceName)
        {
            this.interfaceName = interfaceName;
        }

        public async Task<bool> Disable(CancellationToken token)
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

        public async Task<bool> Enable(CancellationToken token)
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
    }
}
