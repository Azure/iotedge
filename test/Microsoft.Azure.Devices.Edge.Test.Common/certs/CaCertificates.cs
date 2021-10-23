// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Certs
{
    using System.Collections.Generic;
    using System.IO;
    using System.Security.Cryptography.X509Certificates;
    using Microsoft.Azure.Devices.Edge.Util;

    public class CaCertificates : IdCertificates
    {
        public string TrustedCertificatesPath { get; }
        public Option<string> ManifestTrustedCertificatesPath { get; }

        public IEnumerable<X509Certificate2> TrustedCertificates =>
            new[]
            {
                new X509Certificate2(X509Certificate.CreateFromCertFile(this.TrustedCertificatesPath))
            };

        string[] GetEdgeCertFileLocation(string deviceId)
        {
            return new[]
            {
                FixedPaths.DeviceCaCert.Cert(deviceId),
                FixedPaths.DeviceCaCert.Key(deviceId),
                FixedPaths.DeviceCaCert.TrustCert
            };
        }

        public CaCertificates(string deviceId, string scriptPath)
        {
            var location = this.GetEdgeCertFileLocation(deviceId);
            var files = OsPlatform.NormalizeFiles(location, scriptPath);
            this.CertificatePath = files[0];
            this.KeyPath = files[1];
            this.TrustedCertificatesPath = files[2];
        }

        public CaCertificates(string certificatePath, string keyPath, string trustedCertsPath)
        {
            Preconditions.CheckArgument(File.Exists(certificatePath));
            Preconditions.CheckArgument(File.Exists(keyPath));
            Preconditions.CheckArgument(File.Exists(trustedCertsPath));
            this.CertificatePath = certificatePath;
            this.KeyPath = keyPath;
            this.TrustedCertificatesPath = trustedCertsPath;
        }

        public CaCertificates(string certificatePath, string keyPath, string trustedCertsPath, string manifestSigningTrustBundlePath)
        {
            Preconditions.CheckArgument(File.Exists(certificatePath));
            Preconditions.CheckArgument(File.Exists(keyPath));
            Preconditions.CheckArgument(File.Exists(trustedCertsPath));
            this.CertificatePath = certificatePath;
            this.KeyPath = keyPath;
            this.TrustedCertificatesPath = trustedCertsPath;
            this.ManifestTrustedCertificatesPath = Option.Maybe(manifestSigningTrustBundlePath);
        }
    }
}
