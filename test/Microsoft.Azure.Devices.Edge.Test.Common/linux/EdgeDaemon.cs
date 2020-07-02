// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Linux
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.ServiceProcess;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Serilog;

    public class EdgeDaemon : IEdgeDaemon
    {
        Option<string> bootstrapAgentImage;
        Option<Registry> bootstrapRegistry;
        PackageManagement packageManagement;

        public static async Task<EdgeDaemon> CreateAsync(Option<string> bootstrapAgentImage, Option<Registry> bootstrapRegistry)
        {
            PackageManagement packageManagement = await PackageManagement.CreateAsync();
            EdgeDaemon edgeDaemon = new EdgeDaemon(bootstrapAgentImage, bootstrapRegistry, packageManagement);
            return edgeDaemon;
        }

        EdgeDaemon(Option<string> bootstrapAgentImage, Option<Registry> bootstrapRegistry, PackageManagement packageManagement)
        {
            this.bootstrapAgentImage = bootstrapAgentImage;
            this.bootstrapRegistry = bootstrapRegistry;
            this.packageManagement = packageManagement;
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

            string[] commands = packagesPath.Match(
                p =>
                {
                    string[] packages = Directory.GetFiles(p, $"*.{this.packageManagement.PackageExtension.ToString().ToLower()}");
                    for (int i = packages.Length - 1; i >= 0; --i)
                    {
                        if (packages[i].Contains("debug"))
                        {
                            packages[i] = string.Empty;
                        }
                    }

                    switch (this.packageManagement.PackageExtension)
                    {
                        case PackageManagement.SupportedPackageExtension.Deb:
                            return new[]
                                {
                                    "set -e",
                                    $"{this.packageManagement.ForceInstallConfigCmd} {string.Join(' ', packages)}",
                                    $"{this.packageManagement.PackageTool} {this.packageManagement.InstallCmd}"
                                };
                        case PackageManagement.SupportedPackageExtension.Rpm:
                            return new[]
                                {
                                    "set -e",
                                    $"{this.packageManagement.PackageTool} {this.packageManagement.InstallCmd} {string.Join(' ', packages)}",
                                    "pathToSystemdConfig=$(systemctl cat iotedge | head -n 1); sed 's/=on-failure/=no/g' ${pathToSystemdConfig#?} > ~/override.conf; sudo mv -f ~/override.conf ${pathToSystemdConfig#?}; sudo systemctl daemon-reload;"
                                };
                        default:
                            throw new NotImplementedException($"Don't know how to install daemon on for '.{this.packageManagement.PackageExtension}'");
                    }
                },
                () =>
                {
                    switch (this.packageManagement.PackageExtension)
                    {
                        case PackageManagement.SupportedPackageExtension.Deb:
                            // Based on instructions at:
                            // https://github.com/MicrosoftDocs/azure-docs/blob/058084949656b7df518b64bfc5728402c730536a/articles/iot-edge/how-to-install-iot-edge-linux.md
                            // TODO: 8/30/2019 support curl behind a proxy
                            return new[]
                                {
                                    $"curl https://packages.microsoft.com/config/{this.packageManagement.Os}/{this.packageManagement.Version}/multiarch/prod.list > /etc/apt/sources.list.d/microsoft-prod.list",
                                    "curl https://packages.microsoft.com/keys/microsoft.asc | gpg --dearmor > /etc/apt/trusted.gpg.d/microsoft.gpg",
                                    $"{this.packageManagement.PackageTool} update",
                                    $"{this.packageManagement.PackageTool} install --yes iotedge"
                                };
                        case PackageManagement.SupportedPackageExtension.Rpm:
                            return new[]
                                {
                                    $"{this.packageManagement.ForceInstallConfigCmd} https://packages.microsoft.com/config/{this.packageManagement.Os}/{this.packageManagement.Version}/packages-microsoft-prod.rpm",
                                    $"{this.packageManagement.PackageTool} updateinfo",
                                    $"{this.packageManagement.PackageTool} install --yes iotedge",
                                    "pathToSystemdConfig=$(systemctl cat iotedge | head -n 1); sed 's/=on-failure/=no/g' ${pathToSystemdConfig#?} > ~/override.conf; sudo mv -f ~/override.conf ${pathToSystemdConfig#?}; sudo systemctl daemon-reload;"
                                };
                        default:
                            throw new NotImplementedException($"Don't know how to install daemon on for '.{this.packageManagement.PackageExtension}'");
                    }
                });

            await Profiler.Run(
                async () =>
                {
                    string[] output = await Process.RunAsync("bash", $"-c \"{string.Join(" || exit $?; ", commands)}\"", token);
                    Log.Verbose(string.Join("\n", output));

                    await this.InternalStopAsync(token);
                },
                message,
                properties);
        }

        public Task ConfigureAsync(Func<DaemonConfiguration, Task<(string, object[])>> config, CancellationToken token, bool restart)
        {
            var properties = new object[] { };
            var message = "Configured edge daemon";

            return Profiler.Run(
                async () =>
                {
                    await this.InternalStopAsync(token);
                    var yaml = new DaemonConfiguration("/etc/iotedge/config.yaml", this.bootstrapAgentImage, this.bootstrapRegistry);
                    (string msg, object[] props) = await config(yaml);

                    message += $" {msg}";
                    properties = properties.Concat(props).ToArray();

                    if (restart)
                    {
                        await this.InternalStartAsync(token);
                    }
                },
                message.ToString(),
                properties);
        }

        public Task StartAsync(CancellationToken token) => Profiler.Run(
            () => this.InternalStartAsync(token),
            "Started edge daemon");

        async Task InternalStartAsync(CancellationToken token)
        {
            string[] output = await Process.RunAsync("systemctl", "start iotedge", token);
            Log.Verbose(string.Join("\n", output));
            await WaitForStatusAsync(ServiceControllerStatus.Running, token);
        }

        public Task StopAsync(CancellationToken token) => Profiler.Run(
            () => this.InternalStopAsync(token),
            "Stopped edge daemon");

        async Task InternalStopAsync(CancellationToken token)
        {
            string[] output = await Process.RunAsync("systemctl", $"stop {this.packageManagement.IotedgeServices}", token);
            Log.Verbose(string.Join("\n", output));
            await WaitForStatusAsync(ServiceControllerStatus.Stopped, token);
        }

        public async Task UninstallAsync(CancellationToken token)
        {
            try
            {
                await this.InternalStopAsync(token);
            }
            catch (Win32Exception e)
            {
                Log.Verbose(e, "Failed to stop edge daemon, probably because it is already stopped");
            }

            try
            {
                await Profiler.Run(
                    async () =>
                    {
                        string[] output =
                            await Process.RunAsync($"{this.packageManagement.PackageTool}", $"{this.packageManagement.UninstallCmd} libiothsm-std iotedge", token);
                        Log.Verbose(string.Join("\n", output));
                    },
                    "Uninstalled edge daemon");
            }
            catch (Win32Exception e)
            {
                Log.Verbose(e, "Failed to uninstall edge daemon, probably because it isn't installed");
            }
        }

        public Task WaitForStatusAsync(EdgeDaemonStatus desired, CancellationToken token) => Profiler.Run(
            () => WaitForStatusAsync((ServiceControllerStatus)desired, token),
            "Edge daemon entered the '{Desired}' state",
            desired.ToString().ToLower());

        static async Task WaitForStatusAsync(ServiceControllerStatus desired, CancellationToken token)
        {
            while (true)
            {
                Func<string, bool> stateMatchesDesired;
                switch (desired)
                {
                    case ServiceControllerStatus.Running:
                        stateMatchesDesired = s => s == "active";
                        break;
                    case ServiceControllerStatus.Stopped:
                        stateMatchesDesired = s => s == "inactive" || s == "failed";
                        break;
                    default:
                        throw new NotImplementedException($"No handler for {desired.ToString()}");
                }

                string[] output = await Process.RunAsync("systemctl", "-p ActiveState show iotedge", token);
                Log.Verbose(output.First());
                if (stateMatchesDesired(output.First().Split("=").Last()))
                {
                    break;
                }

                await Task.Delay(250, token).ConfigureAwait(false);
            }
        }
    }
}
