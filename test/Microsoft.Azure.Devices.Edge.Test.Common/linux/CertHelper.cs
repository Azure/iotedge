// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Linux
{
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common.Certs;
    using Serilog;

    public static class CertHelper
    {
        public static async Task<EdgeCertificates> GenerateEdgeCertificatesAsync(string deviceId, string scriptPath, CancellationToken token)
        {
            var command = $"'{scriptPath}' create_edge_device_certificate '{deviceId}'";

            await Profiler.Run(
                async () =>
                {
                    string[] output =
                        await Process.RunAsync("bash", $"-c \"{command}\"", token);
                    Log.Verbose(string.Join("\n", output));
                },
                "Created certificates for edge device");

            string dir = new FileInfo(scriptPath).DirectoryName;

            return new EdgeCertificates(
                $"{dir}/certs/iot-edge-device-{deviceId}-full-chain.cert.pem",
                $"{dir}/private/iot-edge-device-{deviceId}.key.pem",
                $"{dir}/certs/azure-iot-test-only.root.ca.cert.pem");
        }

        public static async Task<LeafCertificates> GenerateLeafCertificatesAsync(string leafDeviceId, string scriptPath, CancellationToken token)
        {
            var command = $"'{scriptPath}' create_device_certificate '{leafDeviceId}'";

            await Profiler.Run(
                async () =>
                {
                    string[] output =
                        await Process.RunAsync("bash", $"-c \"{command}\"", token);
                    Log.Verbose(string.Join("\n", output));
                },
                "Created certificates for leaf device");

            string dir = new FileInfo(scriptPath).DirectoryName;

            return new LeafCertificates(
                $"{dir}/certs/iot-device-{leafDeviceId}-full-chain.cert.pem",
                $"{dir}/private/iot-device-{leafDeviceId}.key.pem");
        }

        public static async Task InstallRootCertificateAsync(string certPath, string keyPath, string password, string scriptPath, CancellationToken token)
        {
            var command = $"'{scriptPath}' install_root_ca_from_files '{certPath}' '{keyPath}' '{password}'";
            string[] output = await Process.RunAsync("bash", $"-c \"{command}\"", token);
            Log.Verbose(string.Join("\n", output));
        }
    }
}
