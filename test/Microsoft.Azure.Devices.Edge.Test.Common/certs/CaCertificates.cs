// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Certs
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Security.Cryptography.X509Certificates;
    using Microsoft.Azure.Devices.Edge.Util;

    public class CaCertificates
    {
        static string[] GetFileLocations(string deviceId) =>
            new[]
            {
                FixedPaths.DeviceCaCert.Cert(deviceId),
                FixedPaths.DeviceCaCert.Key(deviceId),
                FixedPaths.DeviceCaCert.TrustCert
            };

        public string CertificatePath { get; }
        public string KeyPath { get; }
        public string TrustedCertificatesPath { get; }

        public IEnumerable<X509Certificate2> TrustedCertificates =>
            new[]
            {
                new X509Certificate2(X509Certificate.CreateFromCertFile(this.TrustedCertificatesPath))
            };

        public CaCertificates(string deviceId, string scriptPath)
        {
            var locations = GetFileLocations(deviceId);
            var files = OsPlatform.NormalizeFiles(locations, scriptPath);
            this.CertificatePath = files[0].FullName;
            this.KeyPath = files[1].FullName;
            this.TrustedCertificatesPath = files[2].FullName;
        }

        public CaCertificates(FileInfo certificatePath, FileInfo keyPath, FileInfo trustedCertsPath)
        {
            Preconditions.CheckArgument(certificatePath.Exists);
            Preconditions.CheckArgument(keyPath.Exists);
            Preconditions.CheckArgument(trustedCertsPath.Exists);
            this.CertificatePath = certificatePath.FullName;
            this.KeyPath = keyPath.FullName;
            this.TrustedCertificatesPath = trustedCertsPath.FullName;
        }

        // Move certs/keys out of default directory so they aren't overwritten
        public static CaCertificates CopyTo(string deviceId, string scriptPath, string destPath)
        {
            var paths = GetFileLocations(deviceId);
            var sourcePaths = OsPlatform.NormalizeFiles(paths, scriptPath);
            var destinationPaths = OsPlatform.NormalizeFiles(paths, destPath, assertExists: false);
            OsPlatform.CopyCertificates(sourcePaths, destinationPaths);
            return new CaCertificates(destinationPaths[0], destinationPaths[1], destinationPaths[2]);
        }
    }
}
