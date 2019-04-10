// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Linq;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using common;
using Microsoft.Azure.Devices.Edge.Util;

namespace common
{
    public enum SecurityDaemonStatus
    {
        Running = ServiceControllerStatus.Running,
        Stopped = ServiceControllerStatus.Stopped
    }

    public class SecurityDaemon
    {
        private string deviceConnectionString;
        private EdgeDeviceIdentity identity;
        private string scriptDir;

        public SecurityDaemon(string scriptDir, EdgeDeviceIdentity identity)
        {
            this.deviceConnectionString = identity.ConnectionString
                .Expect(() => new ArgumentException("Edge device identity has not been created"));
            this.identity = identity;
            this.scriptDir = scriptDir;
        }

        public async Task VerifyModuleIsRunningAsync(string name, CancellationToken token)
        {
            try
            {
                await Retry.Do(
                    async () =>
                    {
                        string[] result = await Process.RunAsync("iotedge", "list", token);

                        return result
                            .Where(ln => ln.Split(null as char[], StringSplitOptions.RemoveEmptyEntries).First() == name)
                            .DefaultIfEmpty("name status")
                            .Single()
                            .Split(null as char[], StringSplitOptions.RemoveEmptyEntries)
                            .ElementAt(1); // second column is STATUS
                    },
                    s => s == "running",
                    e =>
                    {
                        // Retry if iotedged's management endpoint is still starting up,
                        // and therefore isn't responding to `iotedge list` yet
                        bool DaemonNotReady(string details) =>
                            details.Contains("Could not list modules", StringComparison.OrdinalIgnoreCase) ||
                            details.Contains("Socket file could not be found", StringComparison.OrdinalIgnoreCase);
                        return DaemonNotReady(e.ToString()) ? true : false;
                    },
                    TimeSpan.FromSeconds(5),
                    token);
            }
            catch (OperationCanceledException)
            {
                throw new Exception($"Error searching for {name} module: not found");
            }
            catch (Exception e)
            {
                throw new Exception($"Error searching for {name} module: {e}");
            }

            Console.WriteLine($"Edge module '{name}' is running");
        }

        public async Task InstallAsync(CancellationToken token)
        {
            var commands = new string[]
            {
                "$ProgressPreference='SilentlyContinue'",
                $". {this.scriptDir}\\IotEdgeSecurityDaemon.ps1",
                $"Install-IoTEdge -Manual -ContainerOs Windows -DeviceConnectionString '{this.deviceConnectionString}'"
            };
            await Process.RunAsync("powershell", string.Join(";", commands), token);

            Console.WriteLine("Daemon was installed");
        }

        public async Task UninstallAsync(CancellationToken token)
        {
            var commands = new string[]
            {
                "$ProgressPreference='SilentlyContinue'",
                $". {this.scriptDir}\\IotEdgeSecurityDaemon.ps1",
                "Uninstall-IoTEdge -Force"
            };
            await Process.RunAsync("powershell", string.Join(";", commands), token);

            Console.WriteLine("Daemon was uninstalled");
        }

        public async Task WaitForStatusAsync(SecurityDaemonStatus desired, CancellationToken token)
        {
            var sc = new ServiceController("iotedge");
            while (sc.Status != (ServiceControllerStatus)desired)
            {
                await Task.Delay(250, token).ConfigureAwait(false);
                sc.Refresh();
            }

            Console.WriteLine($"Daemon is {desired.ToString().ToLower()}");
        }
    }
}
