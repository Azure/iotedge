// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using System;
    using System.ServiceProcess;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Serilog;

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

            string message = "Installed edge daemon";
            var properties = new object[] { };
            packagesPath.ForEach(p =>
            {
                message += " from packages in '{InstallPackagePath}'";
                properties = new object[] { p };
            });

            return Profiler.Run(
                async () =>
                {
                    string[] output =
                        await Process.RunAsync("powershell", string.Join(";", commands), token);
                    Log.Verbose(string.Join("\n", output));
                },
                message,
                properties);
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
                async () =>
                {
                    string[] output =
                        await Process.RunAsync("powershell", string.Join(";", commands), token);
                    Log.Verbose(string.Join("\n", output));
                },
                "Uninstalled edge daemon");
        }

        public Task StartAsync(CancellationToken token)
        {
            var sc = new ServiceController("iotedge");
            return Profiler.Run(
                async () =>
                {
                    if (sc.Status != ServiceControllerStatus.Running)
                    {
                        sc.Start();
                        await this.WaitForStatusAsync(sc, ServiceControllerStatus.Running, token);
                    }
                },
                "Started edge daemon");
        }

        public Task StopAsync(CancellationToken token)
        {
            var sc = new ServiceController("iotedge");
            return Profiler.Run(
                async () =>
                {
                    if (sc.Status != ServiceControllerStatus.Stopped)
                    {
                        sc.Stop();
                        await this.WaitForStatusAsync(sc, ServiceControllerStatus.Stopped, token);
                    }
                },
                "Stopped edge daemon");
        }

        public Task WaitForStatusAsync(EdgeDaemonStatus desired, CancellationToken token)
        {
            var sc = new ServiceController("iotedge");
            return Profiler.Run(
                () => this.WaitForStatusAsync(sc, (ServiceControllerStatus)desired, token),
                "Edge daemon entered the '{Desired}' state",
                desired.ToString().ToLower());
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
