// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Linux
{
    using System;
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
        readonly PackageManagement packageManagement;

        public static async Task<EdgeDaemon> CreateAsync(CancellationToken token)
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

            SupportedPackageExtension packageExtension;

            switch (os)
            {
                case "ubuntu":
                    packageExtension = SupportedPackageExtension.Deb;
                    break;
                case "raspbian":
                    os = "debian";
                    version = "stretch";
                    packageExtension = SupportedPackageExtension.Deb;
                    break;
                case "centos":
                    version = version.Split('.')[0];
                    packageExtension = SupportedPackageExtension.Rpm;

                    if (version != "7")
                    {
                        throw new NotImplementedException($"Don't know how to install daemon on operating system '{os} {version}'");
                    }

                    break;
                default:
                    throw new NotImplementedException($"Don't know how to install daemon on operating system '{os}'");
            }

            return new EdgeDaemon(new PackageManagement(os, version, packageExtension));
        }

        EdgeDaemon(PackageManagement packageManagement)
        {
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
                p => this.packageManagement.GetInstallCommandsFromLocal(p),
                () => this.packageManagement.GetInstallCommandsFromMicrosoftProd(proxy));

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

                    ConfigFilePaths paths = new ConfigFilePaths
                    {
                        Keyd = "/etc/aziot/keyd/config.toml",
                        Certd = "/etc/aziot/certd/config.toml",
                        Identityd = "/etc/aziot/identityd/config.toml",
                        Edged = "/etc/aziot/edged/config.toml"
                    };

                    DaemonConfiguration conf = new DaemonConfiguration(paths);
                    (string msg, object[] props) = await config(conf);

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
            string[] output = await Process.RunAsync("systemctl", "start aziot-keyd aziot-certd aziot-identityd aziot-edged", token);
            Log.Verbose(string.Join("\n", output));
            await WaitForStatusAsync(ServiceControllerStatus.Running, token);

            // Waiting for the processes to enter the "Running" state doesn't guarantee that
            // they are fully started and ready to accept requests. Therefore, this function
            // must wait until a request can be processed.
            while (true)
            {
                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "iotedge",
                    Arguments = "list",
                    RedirectStandardOutput = true
                };
                var request = System.Diagnostics.Process.Start(processInfo);

                if (request.WaitForExit(1000))
                {
                    if (request.ExitCode == 0)
                    {
                        request.Close();
                        Log.Verbose("aziot-edged ready for requests");
                        break;
                    }
                }
                else
                {
                    request.Kill(true);
                    request.WaitForExit();
                    request.Close();
                    Log.Verbose("aziot-edged not yet ready");
                }
            }
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

            string[] commands = this.packageManagement.GetUninstallCommands();

            foreach (string command in commands)
            {
                try
                {
                    await Profiler.Run(
                    async () =>
                    {
                        string[] output = await Process.RunAsync("bash", $"-c \"{string.Join(" || exit $?; ", command)}\"", token);
                        if (output.Length > 0)
                        {
                            Log.Verbose($"Uninstall command '{command}' ran unsuccessfully. This is probably because this component wasn't installed. Output:\n" + string.Join("\n", output));
                        }
                        else
                        {
                            Log.Verbose($"Uninstall command '{command}' ran successfully");
                        }
                    }, $"Successful: {command}");
                }
                catch (Win32Exception e)
                {
                    Log.Verbose(e, $"Failed to uninstall edge component with command '{command}', probably because this component isn't installed");
                }
            }
        }

        public Task WaitForStatusAsync(EdgeDaemonStatus desired, CancellationToken token) => Profiler.Run(
            () => WaitForStatusAsync((ServiceControllerStatus)desired, token),
            "Edge daemon entered the '{Desired}' state",
            desired.ToString().ToLower());

        static async Task WaitForStatusAsync(ServiceControllerStatus desired, CancellationToken token)
        {
            string[] processes = { "aziot-keyd", "aziot-certd", "aziot-identityd", "aziot-edged" };

            foreach (string process in processes)
            {
                while (true)
                {
                    Func<string, bool> stateMatchesDesired = desired switch
                    {
                        ServiceControllerStatus.Running => s => s == "active",
                        ServiceControllerStatus.Stopped => s => s == "inactive" || s == "failed",
                        _ => throw new NotImplementedException($"No handler for {desired}"),
                    };

                    string[] output = await Process.RunAsync("systemctl", $"-p ActiveState show {process}", token);
                    Log.Verbose($"{process}: {output.First()}");

                    if (stateMatchesDesired(output.First().Split("=").Last()))
                    {
                        break;
                    }

                    await Task.Delay(250, token).ConfigureAwait(false);
                }
            }
        }
    }
}
