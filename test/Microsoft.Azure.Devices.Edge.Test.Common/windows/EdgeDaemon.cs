// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Windows
{
    using System;
    using System.ComponentModel;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.ServiceProcess;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Serilog;

    public class EdgeDaemon : IEdgeDaemon
    {
        Option<string> scriptDir;

        public EdgeDaemon(Option<string> scriptDir)
        {
            this.scriptDir = scriptDir;
        }

        public async Task InstallAsync(Option<string> packagesPath, Option<Uri> proxy, CancellationToken token)
        {
            var properties = new object[] { Dns.GetHostName() };
            string message = "Installed edge daemon on '{Device}'";
            packagesPath.ForEach(
                p =>
                {
                    message += " from packages in '{InstallPackagePath}'";
                    properties = properties.Append(p).ToArray();
                });

            string installCommand = $"Install-IoTEdge -ContainerOs Windows -Manual -DeviceConnectionString 'tbd'";
            packagesPath.ForEach(p => installCommand += $" -OfflineInstallationPath '{p}'");
            proxy.ForEach(
                p => installCommand += $" -InvokeWebRequestParameters @{{ '-Proxy' = '{p}' }}");

            string scriptDir = await this.scriptDir.Match(
                d => Task.FromResult(d),
                () => this.DownloadInstallerAsync(token));

            var commands = new[]
            {
                "$ProgressPreference='SilentlyContinue'",
                $". {scriptDir}\\IotEdgeSecurityDaemon.ps1",
                installCommand
            };

            await Profiler.Run(
                async () => await Process.RunAsync("powershell", string.Join(";", commands), token),
                message,
                properties);
        }

        public async Task ConfigureAsync(Func<DaemonConfiguration, Task<(string, object[])>> config, CancellationToken token, bool restart)
        {
            string configYamlPath =
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) + @"\iotedge\config.yaml";

            Profiler profiler = Profiler.Start();

            await this.InternalStopAsync(token);

            var yaml = new DaemonConfiguration(configYamlPath);
            (string message, object[] properties) = await config(yaml);

            if (restart)
            {
                await this.InternalStartAsync(token);
            }

            profiler.Stop($"Configured edge daemon {message}", properties);
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
                await WaitForStatusAsync(sc, ServiceControllerStatus.Running, token);
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
                // Sometimes Windows will throw ERROR_SERVICE_CANNOT_ACCEPT_CTRL ("The service
                // cannot accept control messages at this time.") when we try to stop a service.
                // When that happens, wait a couple seconds and try again.
                await Retry.Do(
                    () =>
                    {
                        sc.Refresh();
                        sc.Stop();
                        return Task.FromResult(true);
                    },
                    _ => true,
                    e =>
                    {
                        // ERROR_SERVICE_CANNOT_ACCEPT_CTRL
                        if (e is Win32Exception ex && ex.ErrorCode == 1061)
                        {
                            Log.Verbose(
                                "While attempting to stop IoT Edge, Windows returned an error ({Error}). Retrying...",
                                e.Message);
                            return true;
                        }

                        return false;
                    },
                    TimeSpan.FromSeconds(2),
                    token
                );
                await WaitForStatusAsync(sc, ServiceControllerStatus.Stopped, token);
            }
        }

        public async Task UninstallAsync(CancellationToken token)
        {
            string scriptDir = await this.scriptDir.Match(
                d => Task.FromResult(d),
                () => this.DownloadInstallerAsync(token));

            var commands = new[]
            {
                "$ProgressPreference='SilentlyContinue'",
                $". {scriptDir}\\IotEdgeSecurityDaemon.ps1",
                "Uninstall-IoTEdge -Force"
            };

            await Profiler.Run(
                async () => await Process.RunAsync("powershell", string.Join(";", commands), token),
                "Uninstalled edge daemon");
        }

        public Task WaitForStatusAsync(EdgeDaemonStatus desired, CancellationToken token)
        {
            var sc = new ServiceController("iotedge");
            return Profiler.Run(
                () => WaitForStatusAsync(sc, (ServiceControllerStatus)desired, token),
                "Edge daemon entered the '{Desired}' state",
                desired.ToString().ToLower());
        }

        static async Task WaitForStatusAsync(ServiceController sc, ServiceControllerStatus desired, CancellationToken token)
        {
            while (sc.Status != desired)
            {
                await Task.Delay(250, token).ConfigureAwait(false);
                sc.Refresh();
            }
        }

        async Task<string> DownloadInstallerAsync(CancellationToken token)
        {
            const string Address = "aka.ms/iotedge-win";
            string tempDir = Path.GetTempPath();
            string[] commands = new[]
            {
                "$ProgressPreference='SilentlyContinue'",   // don't render PowerShell's progress bar in non-interactive shell
                $"Invoke-WebRequest -UseBasicParsing -OutFile '{Path.Combine(tempDir, "IotEdgeSecurityDaemon.ps1")}' '{Address}'"
            };

            await Profiler.Run(
                async () => await Process.RunAsync("powershell", string.Join(';', commands), token),
                "Downloaded Edge daemon Windows installer from '{Address}'",
                Address);

            this.scriptDir = Option.Some(tempDir);
            return tempDir;
        }
    }
}
