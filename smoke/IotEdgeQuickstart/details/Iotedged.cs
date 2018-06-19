// Copyright (c) Microsoft. All rights reserved.

namespace IotEdgeQuickstart.Details
{
    using System;
    using System.ComponentModel;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    class Iotedged : IBootstrapper
    {
        readonly string archivePath;
        readonly Option<RegistryCredentials> credentials;

        public Iotedged(string archivePath, Option<RegistryCredentials> credentials)
        {
            this.archivePath = archivePath;
            this.credentials = credentials;
        }

        public async Task VerifyNotActive()
        {
            string result = await Process.RunAsync("systemctl", "--no-pager show iotedge | grep ActiveState=");
            if (result.Split("=").Last() == "active")
            {
                throw new Exception("IoT Edge Security Daemon is already active. If you want this test to overwrite the active configuration, please run `systemctl disable --now iotedged` first.");
            }
        }

        public Task VerifyDependenciesAreInstalled() => Task.CompletedTask;

        public Task Install()
        {
            const string PackageName = "iotedge";

            Console.WriteLine($"Installing debian package '{PackageName}' from {this.archivePath ?? "apt"}");

            return Process.RunAsync(
                "apt-get",
                $"--yes install {this.archivePath ?? PackageName}",
                300); // 5 min timeout because install can be slow on raspberry pi
        }

        public async Task Configure(string connectionString, string image, string hostname)
        {
            Console.WriteLine($"Setting up iotedged with container registry '{this.credentials.Match(c => c.Address, () => "<none>")}'");

            const string YamlPath = "/etc/iotedge/config.yaml";
            Task<string> text = File.ReadAllTextAsync(YamlPath);

            var doc = new YamlDocument(await text);
            doc.Replace("provisioning.device_connection_string", connectionString);
            doc.Replace("agent.config.image", image);
            doc.Replace("hostname", hostname);

            foreach (RegistryCredentials c in this.credentials)
            {
                doc.Replace("agent.config.auth.serveraddress", c.Address);
                doc.Replace("agent.config.auth.username", c.User);
                doc.Replace("agent.config.auth.password", c.Password);
            }

            string result = doc.ToString();

            FileAttributes attr = 0;
            if (File.Exists(YamlPath))
            {
                attr = File.GetAttributes(YamlPath);
                File.SetAttributes(YamlPath, attr & ~FileAttributes.ReadOnly);
            }

            await File.WriteAllTextAsync(YamlPath, result);

            if (attr != 0)
            {
                File.SetAttributes(YamlPath, attr);
            }
        }

        public async Task Start()
        {
            try // Remove previous containers with the same name
            {
                await Process.RunAsync("docker", $"rm -f edgeAgent edgeHub tempSensor", 60);
            }
            catch (Win32Exception e) when (e.Message.Contains("No such container"))
            { }

            await Process.RunAsync("systemctl", "enable --now iotedge", 60);
        }

        public Task Stop() => Process.RunAsync("systemctl", "disable --now iotedge", 60);

        public Task Reset() => Task.CompletedTask;
    }
}
