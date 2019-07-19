// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Windows
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Test.Common.Certs;
    using Microsoft.Azure.Devices.Edge.Util;
    using Serilog;

    public class Platform : IPlatform
    {
        public async Task<string> CollectDaemonLogsAsync(DateTime testStartTime, string filePrefix, CancellationToken token)
        {
            string command =
                "Get-WinEvent -ErrorAction SilentlyContinue " +
                $"-FilterHashtable @{{ProviderName='iotedged';LogName='application';StartTime='{testStartTime}'}} " +
                "| Select TimeCreated, Message " +
                "| Sort-Object @{Expression=\'TimeCreated\';Descending=$false} " +
                "| Format-Table -AutoSize -HideTableHeaders " +
                "| Out-String -Width 512";
            string[] output = await Process.RunAsync("powershell", command, token);

            string daemonLog = $"{filePrefix}-iotedged.log";
            await File.WriteAllLinesAsync(daemonLog, output, token);

            return daemonLog;
        }

        public IEdgeDaemon CreateEdgeDaemon(Option<string> installerPath) => new EdgeDaemon(installerPath);

        public async Task<EdgeCertificates> GenerateEdgeCertificatesAsync(string deviceId, string scriptPath, CancellationToken token)
        {
            var commands = new[]
            {
                $". {scriptPath}",
                $"New-CACertsEdgeDevice {deviceId}"
            };

            await Profiler.Run(
                async () =>
                {
                    string[] output =
                        await Process.RunAsync("powershell", string.Join(";", commands), token);
                    Log.Verbose(string.Join("\n", output));
                },
                "Created certificates for edge device");

            string dir = new FileInfo(scriptPath).DirectoryName;

            return new EdgeCertificates(
                $"{dir}\\certs\\iot-edge-device-{deviceId}-full-chain.cert.pem",
                $"{dir}\\private\\iot-edge-device-{deviceId}.key.pem",
                $"{dir}\\certs\\azure-iot-test-only.root.ca.cert.pem");
        }

        public async Task<LeafCertificates> GenerateLeafCertificatesAsync(string leafDeviceId, string scriptPath, CancellationToken token)
        {
            var commands = new[]
            {
                $". {scriptPath}",
                $"New-CACertsDevice '{leafDeviceId}'"
            };

            await Profiler.Run(
                async () =>
                {
                    string[] output =
                        await Process.RunAsync("powershell", string.Join(";", commands), token);
                    Log.Verbose(string.Join("\n", output));
                },
                "Created certificates for leaf device");

            string dir = new FileInfo(scriptPath).DirectoryName;

            return new LeafCertificates(
                $"{dir}\\certs\\iot-device-{leafDeviceId}-full-chain.cert.pem",
                $"{dir}\\private\\iot-device-{leafDeviceId}.key.pem");
        }

        // On Windows, store certs under intermediate CA instead of root CA to avoid security UI in automated tests
        public StoreName GetCertificateStoreName() => StoreName.CertificateAuthority;

        public string GetConfigYamlPath() =>
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) + @"\iotedge\config.yaml";

        public void InstallEdgeCertificates(IEnumerable<X509Certificate2> certs, ITransportSettings transportSettings) =>
            transportSettings.SetupCertificateValidation(certs.First());

        public async Task InstallRootCertificateAsync(string certPath, string keyPath, string password, string scriptPath, CancellationToken token)
        {
            var commands = new[]
            {
                $". {scriptPath}",
                $"Install-RootCACertificate '{certPath}' '{keyPath}' 'rsa' {password}"
            };

            string[] output =
                await Process.RunAsync("powershell", string.Join(";", commands), token);
            Log.Verbose(string.Join("\n", output));
        }
    }
}
