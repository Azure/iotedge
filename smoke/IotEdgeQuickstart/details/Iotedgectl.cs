// Copyright (c) Microsoft. All rights reserved.
namespace IotEdgeQuickstart.Details
{
    using System;
    using System.ComponentModel;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    class Iotedgectl : IBootstrapper
    {
        readonly string archivePath;
        readonly Option<RegistryCredentials> credentials;

        public Iotedgectl(string archivePath, Option<RegistryCredentials> credentials)
        {
            this.archivePath = archivePath;
            this.credentials = credentials;
        }

        public async Task VerifyNotActive()
        {
            try
            {
                await Process.RunAsync("iotedgectl", "status");
            }
            catch (Win32Exception)
            {
                // Should fail for one of two reasons:
                // 1. [ExitCode == 9009] iotedgectl isn't installed
                // 2. [ExitCode == 1] `iotedgectl status` failed because there's no config
                return;
            }

            throw new Exception("IoT Edge runtime is installed. Run `iotedgectl uninstall` before running this test.");
        }

        public Task VerifyDependenciesAreInstalled() => Process.RunAsync("pip", "--version");

        public async Task VerifyModuleIsRunning(string name)
        {
            using (var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10)))
            {
                string errorMessage = null;

                try
                {
                    while (true)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(3), cts.Token);

                        string[] status = await Process.RunAsync(
                            "docker",
                            $"ps --quiet --filter \"name = {name}\"",
                            cts.Token);

                        if (!string.IsNullOrWhiteSpace(status.FirstOrDefault()))
                        {
                            break;
                        }

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
            const string PackageName = "azure-iot-edge-runtime-ctl";

            Console.WriteLine($"Installing python package '{PackageName}' from {this.archivePath ?? "pypi"}");

            return Process.RunAsync(
                "pip",
                $"install --disable-pip-version-check --upgrade {this.archivePath ?? PackageName}",
                300); // 5 min timeout because install can be slow on raspberry pi
        }

        public async Task Configure(DeviceProvisioningMethod method, string image, string hostname, string deviceCaCert, string deviceCaPk, string deviceCaCerts, LogLevel runtimeLogLevel)
        {
            Console.WriteLine($"Setting up iotedgectl with agent image '{image}'");

            string connectionString = method.ManualConnectionString.Expect(() => new ArgumentException("The iotedgectl utility only supports device connection string to bootstrap Edge"));

            string registryArgs = this.credentials.Match(
                c => $"--docker-registries {c.Address} {c.User} {c.Password}",
                () => string.Empty);

            await Process.RunAsync(
                "iotedgectl",
                $"setup --connection-string \"{connectionString}\" --nopass {registryArgs} --image {image} --edge-hostname {hostname}",
                120);
        }

        public Task Start() => Process.RunAsync("iotedgectl", "start", 300); // 5 min timeout because docker pull can be slow on raspberry pi

        public Task Stop() => Process.RunAsync("iotedgectl", "stop", 120);

        public Task Reset() => Process.RunAsync("iotedgectl", "uninstall", 60);
    }
}
