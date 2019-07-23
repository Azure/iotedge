// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common.Certs;
    using Microsoft.Azure.Devices.Edge.Util;
    using Serilog;

    public static class Platform
    {
        public static readonly IPlatform Current = IsWindows() ? new Windows.Platform() as IPlatform : new Linux.Platform();

        public static async Task<EdgeCertificates> GenerateEdgeCertificatesAsync(
            string deviceId,
            string basePath,
            (string name, string args) command,
            CancellationToken token)
        {
            await Profiler.Run(
                async () =>
                {
                    string[] output = await Process.RunAsync(command.name, command.args, token);
                    Log.Verbose(string.Join("\n", output));
                },
                "Created certificates for edge device");

            var files = new[]
            {
                $"certs/iot-edge-device-{deviceId}-full-chain.cert.pem",
                $"private/iot-edge-device-{deviceId}.key.pem",
                "certs/azure-iot-test-only.root.ca.cert.pem"
            };

            files = NormalizeFiles(files, basePath);

            return new EdgeCertificates(files[0], files[1], files[2]);
        }

        public static async Task<LeafCertificates> GenerateLeafCertificatesAsync(
            string leafDeviceId,
            string basePath,
            (string name, string args) command,
            CancellationToken token)
        {
            await Profiler.Run(
                async () =>
                {
                    string[] output = await Process.RunAsync(command.name, command.args, token);
                    Log.Verbose(string.Join("\n", output));
                },
                "Created certificates for leaf device");

            var files = new[]
            {
                $"certs/iot-device-{leafDeviceId}-full-chain.cert.pem",
                $"private/iot-device-{leafDeviceId}.key.pem"
            };

            files = NormalizeFiles(files, basePath);

            return new LeafCertificates(files[0], files[1]);
        }

        public static async Task InstallRootCertificateAsync(
            string basePath,
            (string name, string args) command,
            CancellationToken token)
        {
            string[] output = await Process.RunAsync(command.name, command.args, token);
            Log.Verbose(string.Join("\n", output));

            var files = new[]
            {
                "certs/azure-iot-test-only.root.ca.cert.pem",
                "private/azure-iot-test-only.root.ca.key.pem"
            };

            CheckFiles(files, basePath);
        }

        public static void InstallTrustedCertificates(IEnumerable<X509Certificate2> certs, StoreName storeName)
        {
            using (var store = new X509Store(storeName, StoreLocation.CurrentUser))
            {
                store.Open(OpenFlags.ReadWrite);
                foreach (var cert in certs)
                {
                    store.Add(cert);
                }
            }
        }

        public static bool IsWindows() => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        static void CheckFiles(IEnumerable<string> paths, string basePath) => NormalizeFiles(paths, basePath);

        static string[] NormalizeFiles(IEnumerable<string> paths, string basePath)
        {
            return paths.Select(
                path =>
                {
                    var file = new FileInfo(Path.Combine(basePath, path));
                    Preconditions.CheckArgument(file.Exists);
                    return file.FullName;
                }).ToArray();
        }
    }
}
