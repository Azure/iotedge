// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Linux
{
    using System.Collections.Generic;
    using System.IO;
    using System.Security.Cryptography.X509Certificates;
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

        public static Task<LeafCertificates> GenerateLeafCertificatesAsync(string leafDeviceId, string scriptPath, CancellationToken token)
        {
            throw new System.NotImplementedException();
        }

        public static async Task InstallRootCertificateAsync(string certPath, string keyPath, string password, string scriptPath, CancellationToken token)
        {
            var command = $"'{scriptPath}' install_root_ca_from_files '{certPath}' '{keyPath}' '{password}'";
            string[] output = await Process.RunAsync("bash", $"-c \"{command}\"", token);
            Log.Verbose(string.Join("\n", output));
        }

        public static void InstallTrustedCertificates(IEnumerable<X509Certificate2> certs)
        {
            using (var store = new X509Store(StoreName.Root, StoreLocation.CurrentUser))
            {
                store.Open(OpenFlags.ReadWrite);
                foreach (var cert in certs)
                {
                    store.Add(cert);
                }
            }
        }
    }
}
