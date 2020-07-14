// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Linux
{
    using System;
    using System.ComponentModel;
    using System.Linq;
    using System.Net;
    using System.ServiceProcess;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Serilog;

    public class EdgeDaemon : IEdgeDaemon
    {
        readonly Option<string> bootstrapAgentImage;
        readonly Option<Registry> bootstrapRegistry;
        readonly PackageManagement packageManagement;

        public static async Task<EdgeDaemon> CreateAsync(
            Option<string> bootstrapAgentImage,
            Option<Registry> bootstrapRegistry,
            CancellationToken token)
        {
            string[] platformInfo = await Process.RunAsync("lsb_release", "-sir", token);
            if (platformInfo.Length == 1)
            {
                platformInfo = platformInfo[0].Split(' ');
            }

            string os = platformInfo[0].Trim();
            string version = platformInfo[1].Trim();
            SupportedPackageExtension packageExtension;

            switch (os)
            {
                case "Ubuntu":
                    os = os.ToLower();
                    packageExtension = SupportedPackageExtension.Deb;
                    break;
                case "Raspbian":
                    os = "debian";
                    version = "stretch";
                    packageExtension = SupportedPackageExtension.Deb;
                    break;
                case "CentOS":
                    os = os.ToLower();
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

            return new EdgeDaemon(
                bootstrapAgentImage,
                bootstrapRegistry,
                new PackageManagement(os, version, packageExtension));
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
                p => this.packageManagement.GetInstallCommandsFromLocal(p),
                () => this.packageManagement.GetInstallCommandsFromMicrosoftProd());

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

            string[] commands = this.packageManagement.GetUninstallCommands();

            try
            {
                await Profiler.Run(
                    async () =>
                    {
                        string[] output = await Process.RunAsync("bash", $"-c \"{string.Join(" || exit $?; ", commands)}\"", token);
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
                Func<string, bool> stateMatchesDesired = desired switch
                {
                    ServiceControllerStatus.Running => s => s == "active",
                    ServiceControllerStatus.Stopped => s => s == "inactive" || s == "failed",
                    _ => throw new NotImplementedException($"No handler for {desired}"),
                };
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
