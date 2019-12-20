// Copyright (c) Microsoft. All rights reserved.
namespace IotEdgeQuickstart.Details
{
    using System;
    using System.ComponentModel;
    using System.IO;
    using System.Linq;
    using System.ServiceProcess;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    class IotedgedWindows : IBootstrapper
    {
        readonly string configYamlFile = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) + @"\iotedge\config.yaml";

        readonly string offlineInstallationPath;
        readonly Option<RegistryCredentials> credentials;
        readonly TimeSpan iotEdgeServiceOperationWaitTime = TimeSpan.FromMinutes(5);
        readonly Option<string> proxy;
        readonly Option<UpstreamProtocolType> upstreamProtocol;
        readonly bool requireEdgeInstallation;
        string scriptDir;

        public IotedgedWindows(string offlineInstallationPath, Option<RegistryCredentials> credentials, Option<string> proxy, Option<UpstreamProtocolType> upstreamProtocol, bool requireEdgeInstallation)
        {
            this.offlineInstallationPath = offlineInstallationPath;
            this.credentials = credentials;
            this.proxy = proxy;
            this.upstreamProtocol = upstreamProtocol;
            this.requireEdgeInstallation = requireEdgeInstallation;
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
            using (var cts = new CancellationTokenSource(TimeSpan.FromMinutes(20))) // This long timeout is needed for resource constrained devices pulling the large tempFilterFunctions image
            {
                try
                {
                    string status = string.Empty;

                    await Retry.Do(
                        async () =>
                        {
                            string[] result = await Process.RunAsync("iotedge", "list", cts.Token);
                            WriteToConsole("Output of iotedge list", result);

                            status = result
                                .Where(ln => ln.Split(null as char[], StringSplitOptions.RemoveEmptyEntries).First() == name)
                                .DefaultIfEmpty("name status")
                                .Single()
                                .Split(null as char[], StringSplitOptions.RemoveEmptyEntries)
                                .ElementAt(1); // second column is STATUS

                            return status;
                        },
                        s => s == "running",
                        e =>
                        {
                            // Display error and retry for some transient exceptions such as hyper error
                            string exceptionDetails = e.ToString();
                            if (exceptionDetails.Contains("Could not list modules", StringComparison.OrdinalIgnoreCase) ||
                                exceptionDetails.Contains("Socket file could not be found", StringComparison.OrdinalIgnoreCase))
                            {
                                WriteToConsole("List operation exception caught. Retrying in case iotedge service is still starting...", new[] { exceptionDetails });
                                return true;
                            }

                            return false;
                        },
                        TimeSpan.FromSeconds(5),
                        cts.Token);
                }
                catch (OperationCanceledException)
                {
                    throw new Exception($"Error searching for {name} module: can't be found.");
                }
                catch (Exception e)
                {
                    throw new Exception($"Error searching for {name} module: {e}");
                }
            }
        }

        public Task Install()
        {
            // Windows installation does install + configure in one step. Since we need the connection string
            // to configure and we don't have that information here, we'll do installation in Configure().
            return Task.CompletedTask;
        }

        public async Task Configure(DeviceProvisioningMethod method, string image, string hostname, string deviceCaCert, string deviceCaPk, string deviceCaCerts, LogLevel runtimeLogLevel)
        {
            const string HidePowerShellProgressBar = "$ProgressPreference='SilentlyContinue'";

            Console.WriteLine($"Setting up iotedged with agent image '{image}'");

            using (var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5)))
            {
                if (!string.IsNullOrEmpty(this.offlineInstallationPath))
                {
                    this.scriptDir = File.GetAttributes(this.offlineInstallationPath).HasFlag(FileAttributes.Directory)
                        ? this.offlineInstallationPath
                        : new FileInfo(this.offlineInstallationPath).DirectoryName;
                }
                else
                {
                    this.scriptDir = Path.GetTempPath();
                    await Process.RunAsync(
                        "powershell",
                        $"{HidePowerShellProgressBar}; Invoke-WebRequest -UseBasicParsing -OutFile '{this.scriptDir}\\IotEdgeSecurityDaemon.ps1' aka.ms/iotedge-win",
                        cts.Token);
                }

                string args;
                if (this.requireEdgeInstallation)
                {
                    Console.WriteLine("Installing iotedge...");
                    args = $". {this.scriptDir}\\IotEdgeSecurityDaemon.ps1; Install-SecurityDaemon " +
                           $"-ContainerOs Windows -AgentImage '{image}'";

                    this.proxy.ForEach(proxy => { args += $" -Proxy '{proxy}'"; });

                    if (!string.IsNullOrEmpty(this.offlineInstallationPath))
                    {
                        args += $" -OfflineInstallationPath '{this.offlineInstallationPath}'";
                    }
                }
                else
                {
                    Console.WriteLine("Initializing iotedge...");
                    args = $". {this.scriptDir}\\IotEdgeSecurityDaemon.ps1; Initialize-IoTEdge " +
                           $"-ContainerOs Windows -AgentImage '{image}'";
                }

                args += method.Dps.Map(
                    dps =>
                    {
                        string dpsArgs = string.Empty;
                        dpsArgs += $" -Dps -ScopeId '{dps.ScopeId}'";
                        dps.RegistrationId.ForEach(id => { dpsArgs += $" -RegistrationId '{id}'"; });
                        dps.DeviceIdentityCertificate.ForEach(certPath => { dpsArgs += $" -X509IdentityCertificate '{certPath}'"; });
                        dps.DeviceIdentityPrivateKey.ForEach(pkPath => { dpsArgs += $" -X509IdentityPrivateKey '{pkPath}'"; });
                        dps.SymmetricKey.ForEach(symmKey => { dpsArgs += $" -SymmetricKey '{symmKey}'"; });
                        return dpsArgs;
                    }).GetOrElse(string.Empty);

                // ***************************************************************
                // IMPORTANT: All secret/sensitive argument should be place below.
                // ***************************************************************
                Console.WriteLine($"Run command arguments to configure: {args}");

                args += method.ManualConnectionString.Map(
                    cs => { return $" -Manual -DeviceConnectionString '{cs}'"; }).GetOrElse(string.Empty);

                foreach (RegistryCredentials c in this.credentials)
                {
                    args += $" -Username '{c.User}' -Password (ConvertTo-SecureString '{c.Password}' -AsPlainText -Force)";
                }

                // note: ignore hostname for now
                string[] result = await Process.RunAsync("powershell", $"{HidePowerShellProgressBar}; {args}", cts.Token);
                WriteToConsole("Output from Configure iotedge windows service", result);

                // Stop service and update config file
                await Task.Delay(TimeSpan.FromSeconds(5));
                await this.Stop();

                this.UpdateConfigYamlFile(deviceCaCert, deviceCaPk, deviceCaCerts, runtimeLogLevel);

                // Explicitly set IOTEDGE_HOST environment variable to current process
                this.SetEnvironmentVariable();
            }
        }

        public async Task Start()
        {
            Console.WriteLine("Starting iotedge service.");

            // Configured service is not started up automatically in Windows 10 RS4, but should start up in RS5.
            // Therefore we check if service is not running and start it up explicitly
            try
            {
                var iotedgeService = new ServiceController("iotedge");

                if (iotedgeService.Status != ServiceControllerStatus.Running)
                {
                    iotedgeService.Start();
                    iotedgeService.WaitForStatus(ServiceControllerStatus.Running, this.iotEdgeServiceOperationWaitTime);
                    iotedgeService.Refresh();

                    if (iotedgeService.Status != ServiceControllerStatus.Running)
                    {
                        throw new Exception("Can't start iotedge service within timeout period.");
                    }

                    // Add delay to ensure iotedge service is completely started.
                    await Task.Delay(TimeSpan.FromSeconds(5));
                    Console.WriteLine("iotedge service started.");
                }
                else
                {
                    Console.WriteLine("Iotedge service is already running.");
                }
            }
            catch (Exception e)
            {
                throw new Exception($"Error starting iotedged: {e}");
            }
        }

        public Task Stop()
        {
            Console.WriteLine("Stopping iotedge service.");

            try
            {
                ServiceController[] services = ServiceController.GetServices();
                var iotedgeService = services.FirstOrDefault(s => s.ServiceName.Equals("iotedge", StringComparison.OrdinalIgnoreCase));

                // check service exists
                if (iotedgeService != null)
                {
                    if (iotedgeService.Status != ServiceControllerStatus.Stopped)
                    {
                        iotedgeService.Stop();
                        iotedgeService.WaitForStatus(ServiceControllerStatus.Stopped, this.iotEdgeServiceOperationWaitTime);
                        iotedgeService.Refresh();

                        if (iotedgeService.Status != ServiceControllerStatus.Stopped)
                        {
                            throw new Exception("Can't stop iotedge service within timeout period.");
                        }

                        Console.WriteLine("iotedge service stopped.");
                    }
                    else
                    {
                        Console.WriteLine("Iotedge service is already stopped.");
                    }
                }
                else
                {
                    Console.WriteLine("Iotedge service doesn't exist.");
                }
            }
            catch (Exception e)
            {
                throw new Exception($"Error stopping iotedged: {e}");
            }

            return Task.CompletedTask;
        }

        public Task Reset() => Task.CompletedTask;

        static void WriteToConsole(string header, string[] result)
        {
            Console.WriteLine(header);
            foreach (string r in result)
            {
                Console.WriteLine(r);
            }
        }

        void UpdateConfigYamlFile(string deviceCaCert, string deviceCaPk, string trustBundleCerts, LogLevel runtimeLogLevel)
        {
            string config = File.ReadAllText(this.configYamlFile);
            var doc = new YamlDocument(config);
            doc.ReplaceOrAdd("agent.env.RuntimeLogLevel", runtimeLogLevel.ToString());

            if (!string.IsNullOrEmpty(deviceCaCert) && !string.IsNullOrEmpty(deviceCaPk) && !string.IsNullOrEmpty(trustBundleCerts))
            {
                doc.ReplaceOrAdd("certificates.device_ca_cert", deviceCaCert);
                doc.ReplaceOrAdd("certificates.device_ca_pk", deviceCaPk);
                doc.ReplaceOrAdd("certificates.trusted_ca_certs", trustBundleCerts);
            }

            this.proxy.ForEach(proxy => doc.ReplaceOrAdd("agent.env.https_proxy", proxy));

            this.upstreamProtocol.ForEach(upstreamProtocol => doc.ReplaceOrAdd("agent.env.UpstreamProtocol", upstreamProtocol.ToString()));

            FileAttributes attr = 0;
            attr = File.GetAttributes(this.configYamlFile);
            File.SetAttributes(this.configYamlFile, attr & ~FileAttributes.ReadOnly);

            File.WriteAllText(this.configYamlFile, doc.ToString());

            if (attr != 0)
            {
                File.SetAttributes(this.configYamlFile, attr);
            }
        }

        void SetEnvironmentVariable()
        {
            string config = File.ReadAllText(this.configYamlFile);
            var managementUriRegex = new Regex(@"connect:\s*management_uri:\s*""*(.*)""*");
            Match result = managementUriRegex.Match(config);

            if (result.Groups.Count != 2)
            {
                throw new Exception("can't find management Uri in config file.");
            }

            Console.WriteLine($"Explicitly set environment variable [IOTEDGE_HOST={result.Groups[1].Value}]");
            Environment.SetEnvironmentVariable("IOTEDGE_HOST", result.Groups[1].Value);
        }
    }
}
