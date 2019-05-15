// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common
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
        readonly string scriptDir;

        public EdgeDaemon(string scriptDir)
        {
            this.scriptDir = scriptDir;
        }

        public Task InstallAsync(
            string deviceConnectionString,
            Option<string> packagesPath,
            Option<Uri> proxy,
            CancellationToken token)
        {
            string installCommand = "Install-IoTEdge -Manual -ContainerOs Windows " +
                                    $"-DeviceConnectionString '{deviceConnectionString}'";
            packagesPath.ForEach(p => installCommand += $" -OfflineInstallationPath '{p}'");
            proxy.ForEach(
                p => installCommand += $" -InvokeWebRequestParameters @{{ '-Proxy' = '{p}' }}");

            var commands = new[]
            {
                "$ProgressPreference='SilentlyContinue'",
                $". {this.scriptDir}\\IotEdgeSecurityDaemon.ps1",
                installCommand
            };

            string message = "Installing edge daemon";
            packagesPath.ForEach(p => message += $" from packages in '{p}'");

            return Profiler.Run(
                message,
                () => Process.RunAsync("powershell", string.Join(";", commands), token));
        }

        public Task UninstallAsync(CancellationToken token)
        {
            var commands = new[]
            {
                "$ProgressPreference='SilentlyContinue'",
                $". {this.scriptDir}\\IotEdgeSecurityDaemon.ps1",
                "Uninstall-IoTEdge -Force"
            };
            return Profiler.Run(
                "Uninstalling edge daemon",
                () => Process.RunAsync("powershell", string.Join(";", commands), token));
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
                        await this.WaitForStatusAsync(sc, ServiceControllerStatus.Running, token);
                    }
                });
        }

        public Task StopAsync(CancellationToken token)
        {
            var sc = new ServiceController("iotedge");
            return Profiler.Run(
                "Stopping edge daemon",
                async () =>
                {
                    if (sc.Status != ServiceControllerStatus.Stopped)
                    {
                        sc.Stop();
                        await this.WaitForStatusAsync(sc, ServiceControllerStatus.Stopped, token);
                    }
                });
        }

        public Task WaitForStatusAsync(EdgeDaemonStatus desired, CancellationToken token)
        {
            var sc = new ServiceController("iotedge");
            return Profiler.Run(
                $"Waiting for edge daemon to enter the '{desired.ToString().ToLower()}' state",
                () => this.WaitForStatusAsync(sc, (ServiceControllerStatus)desired, token));
        }

        async Task WaitForStatusAsync(ServiceController sc, ServiceControllerStatus desired, CancellationToken token)
        {
            while (sc.Status != desired)
            {
                await Task.Delay(250, token).ConfigureAwait(false);
                sc.Refresh();
            }
        }
    }
}
