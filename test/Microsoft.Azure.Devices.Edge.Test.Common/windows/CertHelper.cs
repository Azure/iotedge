// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Windows
{
    using System.IO;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Test.Common.Certs;
    using Serilog;

    public static class CertHelper
    {
        public static async Task<EdgeCertificates> GenerateEdgeCertificatesAsync(string deviceId, string scriptPath, CancellationToken token)
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

        public static async Task<LeafCertificates> GenerateLeafCertificatesAsync(string leafDeviceId, string scriptPath, CancellationToken token)
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

        public static async Task InstallRootCertificateAsync(string certPath, string keyPath, string password, string scriptPath, CancellationToken token)
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

        public static void InstallEdgeCertificates(X509Certificate2 cert, ITransportSettings transportSettings) =>
            transportSettings.SetupCertificateValidation(cert);
    }
}
