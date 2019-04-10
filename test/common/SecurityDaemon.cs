// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Threading;
using System.Threading.Tasks;
using common;

namespace common
{
    public class SecurityDaemon
    {
        private string deviceConnectionString;
        private EdgeDeviceIdentity identity;
        private string scriptDir;

        public SecurityDaemon(string scriptDir, EdgeDeviceIdentity identity)
        {
            this.deviceConnectionString = identity.ConnectionString
                .Expect(() => new ArgumentException("Edge device identity has not been created"));
            this.identity = identity;
            this.scriptDir = scriptDir;
        }

        public async Task InstallAsync(CancellationToken token)
        {
            var commands = new string[]
            {
                "$ProgressPreference='SilentlyContinue'",
                $". {this.scriptDir}\\IotEdgeSecurityDaemon.ps1",
                $"Install-IoTEdge -Manual -ContainerOs Windows -DeviceConnectionString '{this.deviceConnectionString}'"
            };
            string[] result = await Process.RunAsync("powershell", string.Join(";", commands), token);
            Console.WriteLine(string.Join("\n", result));
        }

        public async Task UninstallAsync(CancellationToken token)
        {
            var commands = new string[]
            {
                "$ProgressPreference='SilentlyContinue'",
                $". {this.scriptDir}\\IotEdgeSecurityDaemon.ps1",
                "Uninstall-IoTEdge -Force"
            };
            string[] result = await Process.RunAsync("powershell", string.Join(";", commands), token);
            Console.WriteLine(string.Join("\n", result));
        }
    }
}
