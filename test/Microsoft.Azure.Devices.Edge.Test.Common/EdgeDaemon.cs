// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using System;
    using System.ComponentModel;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;
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

        public async Task InstallAsync(
            string deviceConnectionString,
            Option<string> packagesPath,
            Option<Uri> proxy,
            CancellationToken token)
        {
            var properties = new object[] { };
            string message = "Installed edge daemon";
            packagesPath.ForEach(
                p =>
                {
                    message += " from packages in '{InstallPackagePath}'";
                    properties = new object[] { p };
                });

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
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
            else
            {
                string[] commands = await packagesPath.Match(
                    p =>
                    {
                        string[] packages = Directory.GetFiles(p, "*.deb");
                        return Task.FromResult(new[]
                        {
                            "set -e",
                            $"dpkg --force-confnew -i {string.Join(' ', packages)}",
                            "apt-get install -f"
                        });
                    },
                    async () =>
                    {
                        string[] platformInfo = await Process.RunAsync("lsb_release", "-sir", token);
                        string os = platformInfo[0].Trim();
                        string version = platformInfo[1].Trim();
                        switch (os)
                        {
                            case "Ubuntu":
                                return new[]
                                {
                                    "set -e",
                                    $"curl https://packages.microsoft.com/config/ubuntu/{version}/prod.list > /etc/apt/sources.list.d/microsoft-prod.list",
                                    "curl https://packages.microsoft.com/keys/microsoft.asc | gpg --dearmor > /etc/apt/trusted.gpg.d/microsoft.gpg",
                                    "apt-get update",
                                    "apt-get install --yes iotedge"
                                };
                            case "Raspbian":
                                return new[]
                                {
                                    "set -e",
                                    "curl -L https://aka.ms/libiothsm-std-linux-armhf-latest -o libiothsm-std.deb",
                                    "curl -L https://aka.ms/iotedged-linux-armhf-latest -o iotedge.deb",
                                    "dpkg --force-confnew -i libiothsm-std.deb iotedge.deb",
                                    "apt-get install -f"
                                };
                            default:
                                throw new NotImplementedException($"Don't know how to install daemon on operating system '{os}'");
                        }
                    });

                await Profiler.Run(
                    async () =>
                    {
                        string[] output = await Process.RunAsync("bash", $"-c \"{string.Join("; ", commands)}\"");
                        Log.Verbose(string.Join("\n", output));
                    },
                    message,
                    properties);

                string hostname = (await File.ReadAllTextAsync("/proc/sys/kernel/hostname", token)).Trim();
                IotHubConnectionStringBuilder builder = IotHubConnectionStringBuilder.Create(deviceConnectionString);

                await Profiler.Run(
                    async () =>
                    {
                        await this.LinuxStopAsync(token);

                        const string YamlPath = "/etc/iotedge/config.yaml";
                        string text = await File.ReadAllTextAsync(YamlPath, token);

                        var doc = new YamlDocument(text);
                        doc.ReplaceOrAdd("provisioning.device_connection_string", deviceConnectionString);
                        doc.ReplaceOrAdd("hostname", hostname);

                        string result = doc.ToString();

                        FileAttributes attr = 0;
                        if (File.Exists(YamlPath))
                        {
                            attr = File.GetAttributes(YamlPath);
                            File.SetAttributes(YamlPath, attr & ~FileAttributes.ReadOnly);
                        }

                        await File.WriteAllTextAsync(YamlPath, result, token);

                        if (attr != 0)
                        {
                            File.SetAttributes(YamlPath, attr);
                        }

                        await this.LinuxStartAsync(token);
                    },
                    "Configured edge daemon for device '{Device}' registered as '{Id}'",
                    hostname,
                    builder.DeviceId);
            }
        }

        public async Task UninstallAsync(CancellationToken token)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var commands = new[]
                {
                    "$ProgressPreference='SilentlyContinue'",
                    $". {this.scriptDir}\\IotEdgeSecurityDaemon.ps1",
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
            else
            {
                try
                {
                    await this.LinuxStopAsync(token);
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
                                await Process.RunAsync("apt-get", "purge --yes libiothsm-std", token);
                            Log.Verbose(string.Join("\n", output));
                        },
                        "Uninstalled edge daemon");
                }
                catch (Win32Exception e)
                {
                    Log.Verbose(e, "Failed to uninstall edge daemon, probably because it isn't installed");
                }
        }
        }

        public async Task StartAsync(CancellationToken token)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var sc = new ServiceController("iotedge");
                await Profiler.Run(
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
            else
            {
                await Profiler.Run(
                    () => this.LinuxStartAsync(token),
                    "Started edge daemon");
            }
        }

        async Task LinuxStartAsync(CancellationToken token)
        {
            string[] output = await Process.RunAsync("systemctl", "start iotedge", token);
            Log.Verbose(string.Join("\n", output));
            await this.WaitForStatusAsync(null, ServiceControllerStatus.Running, token);
        }

        public async Task StopAsync(CancellationToken token)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var sc = new ServiceController("iotedge");
                await Profiler.Run(
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
            else
            {
                await Profiler.Run(
                    () => this.LinuxStopAsync(token),
                    "Stopped edge daemon");
            }
        }

        async Task LinuxStopAsync(CancellationToken token)
        {
            string[] output = await Process.RunAsync("systemctl", "stop iotedge", token);
            Log.Verbose(string.Join("\n", output));
            await this.WaitForStatusAsync(null, ServiceControllerStatus.Stopped, token);
        }

        public async Task WaitForStatusAsync(EdgeDaemonStatus desired, CancellationToken token)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var sc = new ServiceController("iotedge");
                await Profiler.Run(
                    () => this.WaitForStatusAsync(sc, (ServiceControllerStatus)desired, token),
                    "Edge daemon entered the '{Desired}' state",
                    desired.ToString().ToLower());
            }
            else
            {
                await Profiler.Run(
                    () => this.WaitForStatusAsync(null, (ServiceControllerStatus)desired, token),
                    "Edge daemon entered the '{Desired}' state",
                    desired.ToString().ToLower());
            }
        }

        async Task WaitForStatusAsync(ServiceController sc, ServiceControllerStatus desired, CancellationToken token)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                while (sc.Status != desired)
                {
                    await Task.Delay(250, token).ConfigureAwait(false);
                    sc.Refresh();
                }
            }
            else
            {
                while (true)
                {
                    string activeState;
                    switch (desired)
                    {
                        case ServiceControllerStatus.Running:
                            activeState = "active";
                            break;
                        case ServiceControllerStatus.Stopped:
                            activeState = "inactive";
                            break;
                        default:
                            throw new NotImplementedException($"No handler for {desired.ToString()}");
                    }

                    // TODO: use `systemctl is-active` instead of `systemctl show`
                    string[] output = await Process.RunAsync("bash", "-c \"systemctl --no-pager show iotedge | grep ActiveState=\"", token);
                    Log.Verbose(string.Join("\n", output));
                    if (output.First().Split("=").Last() == activeState)
                    {
                        break;
                    }
                    await Task.Delay(250, token).ConfigureAwait(false);
                }
            }

        }
    }
}
