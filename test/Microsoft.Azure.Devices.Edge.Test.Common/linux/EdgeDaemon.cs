// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Linux
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.IO;
    using System.Linq;
    using System.ServiceProcess;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Serilog;

    public class EdgeDaemon : IEdgeDaemon
    {
        public async Task InstallAsync(string deviceConnectionString, Option<string> packagesPath, Option<Uri> proxy, CancellationToken token)
        {
            await InstallAsync(packagesPath, token);
            await this.ConfigureAsync(deviceConnectionString, proxy, token);
        }

        static async Task InstallAsync(Option<string> packagesPath, CancellationToken token)
        {
            var properties = new object[] { };
            string message = "Installed edge daemon";
            packagesPath.ForEach(
                p =>
                {
                    message += " from packages in '{InstallPackagePath}'";
                    properties = new object[] { p };
                });

            string[] commands = await packagesPath.Match(
                p =>
                {
                    string[] packages = Directory.GetFiles(p, "*.deb");
                    return Task.FromResult(
                        new[]
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
                                "apt-get install -f",
                                "rm libiothsm-std.deb iotedge.deb"
                            };
                        default:
                            throw new NotImplementedException($"Don't know how to install daemon on operating system '{os}'");
                    }
                });

            await Profiler.Run(
                async () =>
                {
                    string[] output = await Process.RunAsync("bash", $"-c \"{string.Join("; ", commands)}\"", token);
                    Log.Verbose(string.Join("\n", output));
                },
                message,
                properties);
        }

        public Task ConfigureAsync(Func<DaemonConfiguration, Task<(string, object[])>> config, CancellationToken token)
        {
            var properties = new List<object>();
            var message = new StringBuilder("Configured edge daemon");

            return Profiler.Run(
                async () =>
                {
                    await this.InternalStopAsync(token);

                    var yaml = new DaemonConfiguration("/etc/iotedge/config.yaml");
                    (string msg, object[] props) = await config(yaml);

                    message.Append($" {msg}");
                    properties.AddRange(props);

                    await this.InternalStartAsync(token);
                },
                message.ToString(),
                properties);
        }

        Task ConfigureAsync(string deviceConnectionString, Option<Uri> proxy, CancellationToken token)
        {
            return this.ConfigureAsync(
                async (config) =>
                {
                    string hostname = (await File.ReadAllTextAsync("/proc/sys/kernel/hostname", token)).Trim();
                    IotHubConnectionStringBuilder builder = IotHubConnectionStringBuilder.Create(deviceConnectionString);

                    string message = "for device '{Device}' registered as '{Id}'";
                    var properties = new object[] { hostname, builder.DeviceId };

                    proxy.ForEach(
                        p =>
                        {
                            message += " with proxy '{ProxyUri}'";
                            properties = properties.Concat(new object[] { p }).ToArray();
                        });

                    config.SetDeviceConnectionString(deviceConnectionString);
                    config.SetDeviceHostname(hostname);
                    proxy.ForEach(config.AddHttpsProxy);
                    config.Update();

                    return (message, properties);
                },
                token);
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
            string[] output = await Process.RunAsync("systemctl", "stop iotedge", token);
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
