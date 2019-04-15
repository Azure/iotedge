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
        private string scriptDir;

        public EdgeDaemon(string scriptDir, string deviceConnectionString)
        {
            this.deviceConnectionString = deviceConnectionString;
            this.scriptDir = scriptDir;
        }

        public Task InstallAsync(CancellationToken token)
        {
            var commands = new string[]
            {
                "$ProgressPreference='SilentlyContinue'",
                $". {this.scriptDir}\\IotEdgeSecurityDaemon.ps1",
                $"Install-IoTEdge -Manual -ContainerOs Windows -DeviceConnectionString '{this.deviceConnectionString}'"
            };
            return Profiler.Run(
                "Installing edge daemon",
                () => Process.RunAsync("powershell", string.Join(";", commands), token)
            );
        }

        public Task UninstallAsync(CancellationToken token)
        {
            var commands = new string[]
            {
                "$ProgressPreference='SilentlyContinue'",
                $". {this.scriptDir}\\IotEdgeSecurityDaemon.ps1",
                "Uninstall-IoTEdge -Force"
            };
            return Profiler.Run(
                "Uninstalling edge daemon",
                () => Process.RunAsync("powershell", string.Join(";", commands), token)
            );
        }

        public Task StopAsync(CancellationToken token)
        {
            var sc = new ServiceController("iotedge");
            return Profiler.Run(
                "Stopping edge daemon",
                async () => {
                    if (sc.Status != ServiceControllerStatus.Stopped)
                    {
                        sc.Stop();
                        await this._WaitForStatusAsync(sc, ServiceControllerStatus.Stopped, token);
                    }
                }
            );
        }

        public Task WaitForStatusAsync(EdgeDaemonStatus desired, CancellationToken token)
        {
            var sc = new ServiceController("iotedge");
            return Profiler.Run(
                $"Waiting for edge daemon to enter the '{desired.ToString().ToLower()}' state",
                () => this._WaitForStatusAsync(sc, (ServiceControllerStatus)desired, token)
            );
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
