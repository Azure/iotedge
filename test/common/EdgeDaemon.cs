// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Linq;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using common;
using Microsoft.Azure.Devices.Edge.Util;

namespace common
{
    public enum EdgeDaemonStatus
    {
        Running = ServiceControllerStatus.Running,
        Stopped = ServiceControllerStatus.Stopped
    }

    public class EdgeDaemon
    {
        private string deviceConnectionString;
        private EdgeDeviceIdentity identity;
        private string scriptDir;

        public EdgeDaemon(string scriptDir, EdgeDeviceIdentity identity)
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
            await Process.RunAsync("powershell", string.Join(";", commands), token);

            Console.WriteLine("Daemon was installed");
        }

        public async Task UninstallAsync(CancellationToken token)
        {
            var commands = new string[]
            {
                "$ProgressPreference='SilentlyContinue'",
                $". {this.scriptDir}\\IotEdgeSecurityDaemon.ps1",
                "Uninstall-IoTEdge -Force"
            };
            await Process.RunAsync("powershell", string.Join(";", commands), token);

            Console.WriteLine("Daemon was uninstalled");
        }

        public async Task StopAsync(CancellationToken token)
        {
            var sc = new ServiceController("iotedge");
            if (sc.Status != ServiceControllerStatus.Stopped)
            {
                sc.Stop();
                await this._WaitForStatusAsync(sc, ServiceControllerStatus.Stopped, token);
            }
            Console.WriteLine($"Daemon is stopped");
        }

        public async Task WaitForStatusAsync(EdgeDaemonStatus desired, CancellationToken token)
        {
            var sc = new ServiceController("iotedge");
            await this._WaitForStatusAsync(sc, (ServiceControllerStatus)desired, token);
            Console.WriteLine($"Daemon is {desired.ToString().ToLower()}");
        }

        async Task _WaitForStatusAsync(ServiceController sc, ServiceControllerStatus desired, CancellationToken token)
        {
            while (sc.Status != desired)
            {
                await Task.Delay(250, token).ConfigureAwait(false);
                sc.Refresh();
            }
        }
    }
}
