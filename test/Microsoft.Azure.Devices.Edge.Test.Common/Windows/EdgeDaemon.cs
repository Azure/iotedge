// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Windows
{
    using System;
    using System.ServiceProcess;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Serilog;

    public class EdgeDaemon : IEdgeDaemon
    {
        readonly string scriptDir;

        public EdgeDaemon(string scriptDir)
        {
            this.scriptDir = scriptDir;
        }

        public async Task InstallAsync(string deviceConnectionString, Option<string> packagesPath, Option<Uri> proxy, CancellationToken token)
        {
            await this.InstallInternalAsync(deviceConnectionString, packagesPath, proxy, token);
            await this.ConfigureAsync(proxy, token);
        }

        Task InstallInternalAsync(string deviceConnectionString, Option<string> packagesPath, Option<Uri> proxy, CancellationToken token)
        {
            var properties = new object[] { };
            string message = "Installed edge daemon";
            packagesPath.ForEach(
                p =>
                {
                    message += " from packages in '{InstallPackagePath}'";
                    properties = new object[] { p };
                });

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

        Task ConfigureAsync(Option<Uri> proxy, CancellationToken token)
        {
            return proxy.ForEachAsync(
                p =>
                {
                    return Profiler.Run(
                        async () =>
                        {
                            await this.InternalStopAsync(token);

                            var yaml = new DaemonConfiguration();
                            yaml.AddHttpsProxy(p);
                            yaml.Update();

                            await this.InternalStartAsync(token);
                        },
                        "Configured edge daemon with proxy '{ProxyUri}'",
                        p);
                });
        }

        public Task StartAsync(CancellationToken token)
        {
            return Profiler.Run(
                () => this.InternalStartAsync(token),
                "Started edge daemon");
        }

        async Task InternalStartAsync(CancellationToken token)
        {
            var sc = new ServiceController("iotedge");
            if (sc.Status != ServiceControllerStatus.Running)
            {
                sc.Start();
                await this.WaitForStatusAsync(sc, ServiceControllerStatus.Running, token);
            }
        }

        public Task StopAsync(CancellationToken token)
        {
            return Profiler.Run(
                () => this.InternalStopAsync(token),
                "Stopped edge daemon");
        }

        async Task InternalStopAsync(CancellationToken token)
        {
            var sc = new ServiceController("iotedge");
            if (sc.Status != ServiceControllerStatus.Stopped)
            {
                sc.Stop();
                await this.WaitForStatusAsync(sc, ServiceControllerStatus.Stopped, token);
            }
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
