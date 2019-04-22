﻿// Copyright (c) Microsoft. All rights reserved.

namespace common
{
    using System;
    using System.ServiceProcess;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    public enum EdgeDaemonStatus
    {
        Running = ServiceControllerStatus.Running,
        Stopped = ServiceControllerStatus.Stopped
    }

    public class EdgeDaemon
    {
        private string scriptDir;

        public EdgeDaemon(string scriptDir)
        {
            this.scriptDir = scriptDir;
        }

        public Task InstallAsync(
            string deviceConnectionString,
            Option<string> packagesPath,
            Option<string> proxy,
            CancellationToken token
        )
        {
            string installCommand = "Install-IoTEdge -Manual -ContainerOs Windows " +
                $"-DeviceConnectionString '{deviceConnectionString}'";
            packagesPath.ForEach(p => installCommand += $" -OfflineInstallationPath '{p}'");
            proxy.ForEach(p =>
                installCommand += $" -InvokeWebRequestParameters @{{ '-Proxy' = '{p}' }}"
            );

            var commands = new string[]
            {
                "$ProgressPreference='SilentlyContinue'",
                $". {this.scriptDir}\\IotEdgeSecurityDaemon.ps1",
                installCommand
            };

            string message = "Installing edge daemon";
            packagesPath.ForEach(p => message += $" from packages in '{p}'");

            return Profiler.Run(
                message,
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

        public Task StartAsync(CancellationToken token)
        {
            var sc = new ServiceController("iotedge");
            return Profiler.Run(
                "Starting edge daemon",
                async () =>
                {
                    if (sc.Status != ServiceControllerStatus.Running)
                    {
                        sc.Start();
                        await this._WaitForStatusAsync(sc, ServiceControllerStatus.Running, token);
                    }
                }
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
