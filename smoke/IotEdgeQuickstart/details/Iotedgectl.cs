// Copyright (c) Microsoft. All rights reserved.

namespace IotEdgeQuickstart.Details
{
    using System;
    using System.ComponentModel;
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

        public Task Install()
        {
            const string PackageName = "azure-iot-edge-runtime-ctl";

            Console.WriteLine($"Installing python package '{PackageName}' from {this.archivePath ?? "pypi"}");

            return Process.RunAsync(
                "pip",
                $"install --disable-pip-version-check --upgrade {this.archivePath ?? PackageName}",
                300); // 5 min timeout because install can be slow on raspberry pi
        }

        public async Task Configure(string connectionString, string image, string hostname)
        {
            Console.WriteLine($"Setting up iotedgectl with container registry '{this.credentials.Match(c => c.Address, () => "<none>")}'");

            string registryArgs = this.credentials.Match(
                c => $"--docker-registries {c.Address} {c.User} {c.Password}",
                () => string.Empty
            );

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
