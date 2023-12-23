// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using System;
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

        public static FileInfo[] NormalizeFiles(IEnumerable<string> paths, string basePath, bool assertExists = true) =>
            paths.Select(path =>
            {
                var file = new FileInfo(Path.Combine(basePath, path));
                if (assertExists)
                {
                    Preconditions.CheckArgument(file.Exists, $"File Not Found: {file.FullName}");
                }

                return file;
            }).ToArray();

        public static void CopyCertificates(FileInfo[] sourcePaths, FileInfo[] destinationPaths)
        {
            Preconditions.CheckArgument(sourcePaths.Length == destinationPaths.Length);
            for (int i = 0; i < sourcePaths.Length; i++)
            {
                var parentDir = Directory.GetParent(destinationPaths[i].FullName);
                if (!parentDir.Exists)
                {
                    parentDir.Create();
                }

                sourcePaths[i].CopyTo(destinationPaths[i].FullName, overwrite: true);
                switch (destinationPaths[i])
                {
                    case var path when path.Name.EndsWith("key.pem"):
                        OsPlatform.Current.SetOwner(path.FullName, "aziotks", "600");
                        break;
                    case var path when path.Name.EndsWith("cert.pem"):
                        OsPlatform.Current.SetOwner(path.FullName, "aziotcs", "644");
                        break;
                    case var path:
                        throw new NotImplementedException($"Expected file {path} to end with 'key.pem' or 'cert.pem'");
                }
            }
        }
    }
}
