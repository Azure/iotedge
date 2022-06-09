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

    public class OsPlatform
    {
        public static readonly IOsPlatform Current = new Linux.OsPlatform();

        public static bool Is64Bit() => RuntimeInformation.OSArchitecture == Architecture.X64 || RuntimeInformation.OSArchitecture == Architecture.Arm64;

        public static bool IsArm() => RuntimeInformation.OSArchitecture == Architecture.Arm || RuntimeInformation.OSArchitecture == Architecture.Arm64;

        public CaCertificates GetEdgeQuickstartCertificates(string deviceId) =>
            new CaCertificates(
                    FixedPaths.QuickStartCaCert.Cert(deviceId),
                    FixedPaths.QuickStartCaCert.Key(deviceId),
                    FixedPaths.QuickStartCaCert.TrustCert(deviceId));

        protected async Task InstallRootCertificateAsync(
            string basePath,
            (string name, string args) command,
            CancellationToken token)
        {
            await Process.RunAsync(command.name, command.args, token);

            var files = new[]
            {
                FixedPaths.RootCaCert.Cert,
                FixedPaths.RootCaCert.Key
            };

            CheckFiles(files, basePath);
        }

        protected void InstallTrustedCertificates(IEnumerable<X509Certificate2> certs, StoreName storeName)
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

        protected async Task RunScriptAsync(
            (string name, string args) command,
            CancellationToken token)
        {
            Log.Verbose("Executing: " + command.name + ' ' + command.args);
            await Process.RunAsync(command.name, command.args, token);
        }

        static void CheckFiles(IEnumerable<string> paths, string basePath) => NormalizeFiles(paths, basePath);

        public static string[] NormalizeFiles(IEnumerable<string> paths, string basePath)
        {
            return paths.Select(
                path =>
                {
                    var file = new FileInfo(Path.Combine(basePath, path));
                    Preconditions.CheckArgument(file.Exists, $"File Not Found: {file.FullName}");
                    return file.FullName;
                }).ToArray();
        }
    }
}
