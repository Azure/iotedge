// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Windows
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.ServiceProcess;
    using System.Text;
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

        public async Task InstallAsync(string deviceConnectionString, Option<string> packagesPath, Option<Uri> proxy, CancellationToken token)
        {
            await this.InstallInternalAsync(deviceConnectionString, packagesPath, proxy, token);
            await this.ConfigureAsync(proxy, token);
        }

        async Task InstallInternalAsync(string deviceConnectionString, Option<string> packagesPath, Option<Uri> proxy, CancellationToken token)
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
                async () =>
                {
                    string[] output =
                        await Process.RunAsync("powershell", string.Join(";", commands), token);
                    Log.Verbose(string.Join("\n", output));
                },
                message,
                properties);
        }

        public Task ConfigureAsync(Func<DaemonConfiguration, Task<(string, object[])>> config, CancellationToken token)
        {
            var properties = new List<object>();
            var message = new StringBuilder("Configured edge daemon");
            string configYamlPath =
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) + @"\iotedge\config.yaml";

            return Profiler.Run(
                async () =>
                {
                    await this.InternalStopAsync(token);

                    var yaml = new DaemonConfiguration(configYamlPath);
                    (string m, object[] p) = await config(yaml);

                    message.Append($" {m}");
                    properties.AddRange(p);

                    await this.InternalStartAsync(token);
                },
                message.ToString(),
                properties);
        }

        Task ConfigureAsync(Option<Uri> proxy, CancellationToken token)
        {
            return proxy.ForEachAsync(
                p =>
                {
                    return this.ConfigureAsync(
                        config =>
                        {
                            config.AddHttpsProxy(p);
                            config.Update();

                            return Task.FromResult(("with proxy '{ProxyUri}'", new object[] { p.ToString() }));
                        },
                        token);
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
                sc.Stop();
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
                async () =>
                {
                    await Process.RunAsync(
                        "powershell",
                        string.Join(';', commands),
                        token);
                },
                "Downloaded Edge daemon Windows installer from '{Address}'",
                Address);

            this.scriptDir = Option.Some(tempDir);
            return tempDir;
        }
    }
}
