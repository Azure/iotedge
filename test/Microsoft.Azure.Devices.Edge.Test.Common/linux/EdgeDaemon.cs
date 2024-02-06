// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Linux
{
    using System;
    using System.ComponentModel;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Serilog;

    public class EdgeDaemon : IEdgeDaemon
    {
        readonly PackageManagement packageManagement;
        readonly Option<string> packagesPath;
        readonly IServiceManager serviceManager;
        readonly bool isCentOs;
        readonly string certsPath;

        public static async Task<EdgeDaemon> CreateAsync(Option<string> packagesPath, CancellationToken token)
        {
            string[] platformInfo = await Process.RunAsync("cat", @"/etc/os-release", token);
            string os = Array.Find(platformInfo, element => element.StartsWith("ID="));
            string version = Array.Find(platformInfo, element => element.StartsWith("VERSION_ID="));

            // VERSION_ID is desired but it is an optional field
            if (version == null)
            {
                version = Array.Find(platformInfo, element => element.StartsWith("VERSION="));
            }

            if (os == null || version == null)
            {
                throw new NotImplementedException("Failed to gather operating system information from /etc/os-release file");
            }

            // Trim potential whitespaces and double quotes
            char[] trimChr = { ' ', '"' };
            os = os.Split('=').Last().Trim(trimChr).ToLower();
            // Split potential version description (in case VERSION_ID was not available, the VERSION line can contain e.g. '7 (Core)')
            version = version.Split('=').Last().Split(' ').First().Trim(trimChr);

            bool detectedSnap = packagesPath.Map(path => Directory.GetFiles(path, $"*.snap").Length != 0).OrDefault();

            SupportedPackageExtension packageExtension;

            switch (os)
            {
                case "ubuntu":
                    // if we find .deb and .snap files on an Ubuntu 22.04 host, prefer snap
                    packageExtension = detectedSnap && version == "22.04"
                        ? SupportedPackageExtension.Snap
                        : SupportedPackageExtension.Deb;
                    break;
                case "raspbian":
                    os = "debian";
                    version = "stretch";
                    packageExtension = SupportedPackageExtension.Deb;
                    break;
                case "rhel":
                    version = version.Split('.')[0];
                    packageExtension = SupportedPackageExtension.Rpm;

                    if (version != "8" && version != "9")
                    {
                        throw new NotImplementedException($"Operating system '{os} {version}' not supported");
                    }

                    break;
                case "centos":
                    version = version.Split('.')[0];
                    packageExtension = SupportedPackageExtension.Rpm;

                    if (version != "7")
                    {
                        throw new NotImplementedException($"Operating system '{os} {version}' not supported");
                    }

                    break;
                case "mariner":
                    packageExtension = SupportedPackageExtension.Rpm;
                    break;
                default:
                    throw new NotImplementedException($"Don't know how to install daemon on operating system '{os}'");
            }

            if (detectedSnap && packageExtension != SupportedPackageExtension.Snap)
            {
                throw new NotImplementedException(
                    $"Snap package was detected but isn't supported on operating system '{os} {version}'");
            }

            return new EdgeDaemon(packagesPath, new PackageManagement(os, version, packageExtension), os == "centos");
        }

        EdgeDaemon(Option<string> packagesPath, PackageManagement packageManagement, bool isCentOs)
        {
            this.packagesPath = packagesPath;
            this.packageManagement = packageManagement;
            this.serviceManager = packageManagement.PackageExtension == SupportedPackageExtension.Snap
                ? new SnapServiceManager()
                : new SystemdServiceManager();
            this.isCentOs = isCentOs;
            this.certsPath = Path.Combine(Path.GetDirectoryName(this.serviceManager.ConfigurationPath()), "e2e_tests");
        }

        public async Task InstallAsync(Option<Uri> proxy, CancellationToken token)
        {
            var properties = new object[] { Dns.GetHostName() };
            string message = "Installed edge daemon on '{Device}'";
            this.packagesPath.ForEach(
                p =>
                {
                    message += " from packages in '{InstallPackagePath}'";
                    properties = properties.Append(p).ToArray();
                });

            string[] commands = this.packagesPath.Match(
                p => this.packageManagement.GetInstallCommandsFromLocal(p),
                () => this.packageManagement.GetInstallCommandsFromMicrosoftProd(proxy));

            await Profiler.Run(
                async () =>
                {
                    await Process.RunAsync("bash", $"-c \"{string.Join(" || exit $?; ", commands)}\"", token);
                    await this.InternalStopAsync(token);
                },
                message,
                properties);
        }

        public Task ConfigureAsync(
            Func<DaemonConfiguration, Task<(string, object[])>> config,
            CancellationToken token,
            bool restart)
        {
            var properties = new object[] { };
            var message = "Configured edge daemon";

            return Profiler.Run(
                async () =>
                {
                    await this.InternalStopAsync(token);

                    var conf = new DaemonConfiguration(this.serviceManager.ConfigurationPath());
                    if (this.isCentOs)
                    {
                        // The recommended way to set up [listen] sockets in config.toml is to use the 'fd://...' URL
                        // scheme, which will make use of systemd socket activation. CentOS 7 supports systemd but does
                        // not support socket activation, so for that platform use the 'unix://...' scheme.
                        conf.SetConnectSockets("unix:///var/lib/iotedge/workload.sock", "unix:///var/lib/iotedge/mgmt.sock");
                        conf.SetListenSockets("unix:///var/lib/iotedge/workload.sock", "unix:///var/lib/iotedge/mgmt.sock");
                    }

                    if (this.packageManagement.PackageExtension == SupportedPackageExtension.Snap)
                    {
                        conf.SetDeviceHomedir("/var/snap/azure-iot-edge/common/var/lib/aziot/edged");
                        conf.SetMobyRuntimeUri("unix:///var/snap/azure-iot-edge/common/docker-proxy.sock");
                        conf.AddAgentUserId("0");
                    }

                    (string msg, object[] props) = await config(conf);
                    message += $" {msg}";
                    properties = properties.Concat(props).ToArray();

                    if (restart)
                    {
                        await this.serviceManager.ConfigureAsync(token);
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
            await this.serviceManager.StartAsync(token);
            await Task.Delay(10000);

            await Retry.Do(
                async () =>
                {
                    string[] output = await this.GetCli().RunAsync("list", token);
                    return output;
                },
                output => true,
                e =>
                {
                    Log.Warning($"Failed to list iotedge modules.\nException: {e.ToString()}");

                    // Retry if iotedged's management endpoint is still starting up,
                    // and therefore isn't responding to `iotedge list` yet
                    static bool DaemonNotReady(string details) =>
                        details.Contains("Incorrect function", StringComparison.OrdinalIgnoreCase) ||
                        details.Contains("Could not list modules", StringComparison.OrdinalIgnoreCase) ||
                        details.Contains("Operation not permitted", StringComparison.OrdinalIgnoreCase) ||
                        details.Contains("Socket file could not be found", StringComparison.OrdinalIgnoreCase) ||
                        details.Contains("Object reference not set to an instance of an object", StringComparison.OrdinalIgnoreCase);

                    return DaemonNotReady(e.ToString());
                },
                TimeSpan.FromSeconds(5),
                token);
        }

        public Task StopAsync(CancellationToken token) => Profiler.Run(
            () => this.InternalStopAsync(token),
            "Stopped edge daemon");

        async Task InternalStopAsync(CancellationToken token)
        {
            await this.serviceManager.StopAsync(token);
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

            string[] commands = this.packageManagement.GetUninstallCommands();

            await Profiler.Run(
                async () =>
                {
                    foreach (string command in commands)
                    {
                        try
                        {
                            await Process.RunAsync("bash", $"-c \"{string.Join(" || exit $?; ", command)}\"", token);
                        }
                        catch (Win32Exception e)
                        {
                            Log.Verbose(e, $"Failed to uninstall edge component with command '{command}', probably because this component isn't installed");
                        }
                    }
                }, "Uninstalled edge daemon");
        }

        public string GetCertificatesPath() => this.certsPath;

        public IotedgeCli GetCli()
        {
            return new IotedgeCli(this.serviceManager.GetCliName());
        }
    }
}
