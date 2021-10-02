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

    public class OsPlatform : Common.OsPlatform, IOsPlatform
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
            string[] output = await Process.RunAsync("powershell", command, token, logVerbose: false);

            string daemonLog = $"{filePrefix}-iotedged.log";
            await File.WriteAllLinesAsync(daemonLog, output, token);

            return daemonLog;
        }

        public Task<IEdgeDaemon> CreateEdgeDaemonAsync(
            Option<string> installerPath,
            CancellationToken _) =>
            Task.FromResult(new EdgeDaemon(installerPath) as IEdgeDaemon);

        public async Task<IdCertificates> GenerateIdentityCertificatesAsync(string deviceId, string scriptPath, CancellationToken token)
        {
            var command = BuildCertCommand($"New-CACertsDevice '{deviceId}'", scriptPath);
            await this.RunScriptAsync(("powershell", command), token);

            // Windows requires all the certificates from root up to leaf to be installed.
            await this.RunScriptAsync(("powershell", $"Import-Certificate -CertStoreLocation 'cert:\\LocalMachine\\Root' -FilePath " + Path.Combine(scriptPath, "certs", "azure-iot-test-only.root.ca.cert.pem") + " | Out-Host"), token);
            await this.RunScriptAsync(("powershell", $"Import-Certificate -CertStoreLocation 'cert:\\LocalMachine\\Root' -FilePath " + Path.Combine(scriptPath, "certs", "azure-iot-test-only.intermediate.cert.pem") + " | Out-Host"), token);

            return new IdCertificates(deviceId, scriptPath);
        }

        public async Task<CaCertificates> GenerateCaCertificatesAsync(string deviceId, string scriptPath, CancellationToken token)
        {
            string command = BuildCertCommand(
                $"New-CACertsEdgeDevice '{deviceId}'",
                scriptPath);
            await this.RunScriptAsync(("powershell", command), token);
            return new CaCertificates(deviceId, scriptPath);
        }

        public CaCertificates GetEdgeQuickstartCertificates()
        {
            string certsBasePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                @"iotedge\hsm");
            return this.GetEdgeQuickstartCertificates(certsBasePath);
        }

        public void InstallCaCertificates(IEnumerable<X509Certificate2> certs, ITransportSettings transportSettings) =>
            transportSettings.SetupCertificateValidation(certs.First());

        public Task InstallRootCertificateAsync(string certPath, string keyPath, string password, string scriptPath, CancellationToken token)
        {
            string command = BuildCertCommand(
                $"Install-RootCACertificate '{certPath}' '{keyPath}' 'rsa' {password}",
                scriptPath);

            return this.InstallRootCertificateAsync(scriptPath, ("powershell", command), token);
        }

        public void InstallTrustedCertificates(IEnumerable<X509Certificate2> certs) =>
            // On Windows, store certs under intermediate CA instead of root CA to avoid security UI in automated tests
            this.InstallTrustedCertificates(certs, StoreName.CertificateAuthority);

        static string BuildCertCommand(string command, string scriptPath)
        {
            var commands = new[]
            {
                $". {Path.Combine(scriptPath, "ca-certs.ps1")}",
                command
            };

            return string.Join(";", commands);
        }
    }
}
