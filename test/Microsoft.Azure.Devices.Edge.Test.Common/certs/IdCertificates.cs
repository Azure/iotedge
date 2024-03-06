// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Certs
{
    using System.IO;
    using System.Security.Cryptography.X509Certificates;
    using Microsoft.Azure.Devices.Edge.Util;

    public class IdCertificates
    {
        static string[] GetFileLocations(string deviceId) =>
            new[]
            {
                FixedPaths.DeviceIdentityCert.Cert(deviceId),
                FixedPaths.DeviceIdentityCert.Key(deviceId)
            };

        public string CertificatePath { get; protected set; }
        public string KeyPath { get; protected set; }

        public X509Certificate2 Certificate => new X509Certificate2(this.CertificatePath);

        public IdCertificates(string deviceId, string scriptPath)
        {
            var locations = GetFileLocations(deviceId);
            var files = OsPlatform.NormalizeFiles(locations, scriptPath);
            this.CertificatePath = files[0].FullName;
            this.KeyPath = files[1].FullName;
        }

        public IdCertificates(FileInfo certificatePath, FileInfo keyPath)
        {
            Preconditions.CheckArgument(certificatePath.Exists);
            Preconditions.CheckArgument(keyPath.Exists);
            this.CertificatePath = certificatePath.FullName;
            this.KeyPath = keyPath.FullName;
        }

        // Move certs/keys out of default directory so they aren't overwritten
        public static IdCertificates CopyTo(string deviceId, string scriptPath, string destPath)
        {
            var paths = GetFileLocations(deviceId);
            var sourcePaths = OsPlatform.NormalizeFiles(paths, scriptPath);
            var destinationPaths = OsPlatform.NormalizeFiles(paths, destPath, assertExists: false);
            OsPlatform.CopyCertificates(sourcePaths, destinationPaths);
            return new IdCertificates(destinationPaths[0], destinationPaths[1]);
        }
    }
}
