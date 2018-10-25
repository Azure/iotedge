namespace IotEdgeQuickstart.Details
{
    using System;
    using System.ComponentModel;
    using System.IO;
    using System.Linq;
    using System.ServiceProcess;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    class IotedgedWindows : IBootstrapper
    {
        readonly string archivePath;
        readonly Option<RegistryCredentials> credentials;
        readonly Option<string> proxy;
        string scriptDir;

        public IotedgedWindows(string archivePath, Option<RegistryCredentials> credentials, Option<string> proxy)
        {
            this.archivePath = archivePath;
            this.credentials = credentials;
            this.proxy = proxy;
        }

        public async Task VerifyNotActive()
        {
            bool active = true;
            try
            {
                await Process.RunAsync("sc", "query iotedge");
            }
            catch (Win32Exception e) when (e.NativeErrorCode == 1060)
            {
                // The specified service does not exist as an installed service.
                active = false;
            }

            if (active)
            {
                throw new Exception("IoT Edge Security Daemon is already installed and may have an active configuration. If you want this run this test, please uninstall the daemon first.");
            }
        }

        public Task VerifyDependenciesAreInstalled() => Task.CompletedTask;

        public async Task VerifyModuleIsRunning(string name)
        {
            using (var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5)))
            {
                string errorMessage = null;

                try
                {
                    while (true)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(3), cts.Token);

                        string[] result = await Process.RunAsync("iotedge", "list", cts.Token);
                        string status = result
                            .Where(ln => ln.Split(null as char[], StringSplitOptions.RemoveEmptyEntries).First() == name)
                            .DefaultIfEmpty("name status")
                            .Single()
                            .Split(null as char[], StringSplitOptions.RemoveEmptyEntries)
                            .ElementAt(1);  // second column is STATUS

                        if (status == "running") break;

                        errorMessage = "Not found";
                    }
                }
                catch (OperationCanceledException e)
                {
                    throw new Exception($"Error searching for {name} module: {errorMessage ?? e.Message}");
                }
                catch (Exception e)
                {
                    throw new Exception($"Error searching for {name} module: {e.Message}");
                }
            }
        }

        public Task Install()
        {
            // Windows installation does install + configure in one step. Since we need to connection string
            // to configure and we don't have that information here, we'll do installation in Configure().

            return Task.CompletedTask;
        }


        public async Task Configure(string connectionString, string image, string hostname, string deviceCaCert, string deviceCaPk, string deviceCaCerts, LogLevel runtimeLogLevel)
        {
            Console.WriteLine($"Installing iotedged from {this.archivePath ?? "default location"}");
            Console.WriteLine($"Setting up iotedged with agent image '{image}'");

            using (var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5)))
            {
                if (!string.IsNullOrEmpty(this.archivePath))
                {
                    this.scriptDir = File.GetAttributes(this.archivePath).HasFlag(FileAttributes.Directory)
                        ? this.archivePath
                        : new FileInfo(this.archivePath).DirectoryName;
                }
                else
                {
                    this.scriptDir = Path.GetTempPath();
                    await Process.RunAsync("powershell",
                        $"iwr -useb -o '{this.scriptDir}\\IotEdgeSecurityDaemon.ps1' aka.ms/iotedge-win",
                        cts.Token);
                }

                string args = $". {this.scriptDir}\\IotEdgeSecurityDaemon.ps1; Install-SecurityDaemon -Manual " +
                    $"-ContainerOs Windows -DeviceConnectionString '{connectionString}' -AgentImage '{image}'";

                foreach (RegistryCredentials c in this.credentials)
                {
                    args += $" -Username '{c.User}' -Password (ConvertTo-SecureString '{c.Password}' -AsPlainText -Force)";
                }

                this.proxy.ForEach(proxy => {
                    args += $" -Proxy '{proxy}'";
                });

                if (this.archivePath != null)
                {
                    args += $" -ArchivePath '{this.archivePath}'";
                }

                // note: ignore hostname for now

                await Process.RunAsync("powershell", args, cts.Token);
            }
        }

        public async Task Start()
        {
            Console.WriteLine("Starting up iotedge service on Windows");

            // Configured service is not started up automatically in Windows 10 RS4, but should start up in RS5.
            // Therefore we check if service is not running and start it up explicitly
            try
            {
                ServiceController iotedgeService = ServiceController.GetServices().Single(s => s.ServiceName == "iotedge");

                if (iotedgeService.Status != ServiceControllerStatus.Running)
                {
                    using (var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2)))
                    {
                        iotedgeService.Start();

                        // Wait for service to become active
                        while (!cts.Token.IsCancellationRequested && iotedgeService.Status != ServiceControllerStatus.Running)
                        {
                            await Task.Delay(TimeSpan.FromSeconds(3), cts.Token);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                throw new Exception($"Error starting iotedged: {e}");
            }
        }

        public async Task Stop()
        {
            await Process.RunAsync("powershell", "Stop-Service -NoWait iotedge");
            await Task.Delay(TimeSpan.FromSeconds(3));
        }

        public async Task Reset()
        {
            using (var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3)))
            {
                await Process.RunAsync("powershell",
                    $". {this.scriptDir}\\IotEdgeSecurityDaemon.ps1; Uninstall-SecurityDaemon",
                    cts.Token);
            }
        }
    }
}
