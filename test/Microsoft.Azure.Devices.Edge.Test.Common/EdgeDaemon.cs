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
            string message = "Installed edge daemon";
            packagesPath.ForEach(p => { message += $" from packages in '{p}'"; });

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

                var properties = new object[] { };
                packagesPath.ForEach(p => properties = new object[] { p });

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
                string[] packages = new string[] { };
                packagesPath.ForEach(p => packages = Directory.GetFiles(p, "*.deb"));

                await Profiler.Run(
                    async () =>
                    {
                        string[] output = await Process.RunAsync("dpkg", $"--force-confnew -i {string.Join(' ', packages)}", token);
                        Log.Verbose(string.Join("\n", output));
                    },
                    message);

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
                    $"Configured edge daemon for edge device '{builder.DeviceId}', hostname '{hostname}'");
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
                    await this.StopAsync(token);

                    await Profiler.Run(
                        async () =>
                        {
                            string[] output =
                                await Process.RunAsync("apt-get", "apt-get purge libiothsm-std --yes", token);
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

                    string[] output = await Process.RunAsync("bash", "-c \"systemctl --no-pager show iotedge | grep ActiveState=\"");
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
