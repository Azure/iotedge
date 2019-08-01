// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Certs
{
    using System.Collections.Generic;
    using System.IO;
    using System.Security.Cryptography.X509Certificates;
    using Microsoft.Azure.Devices.Edge.Util;

    public class EdgeCertificates
    {
        public string CertificatePath { get; }

        public string KeyPath { get; }

        public string TrustedCertificatesPath { get; }

        public IEnumerable<X509Certificate2> TrustedCertificates =>
            new[]
            {
                new X509Certificate2(X509Certificate.CreateFromCertFile(this.TrustedCertificatesPath))
            };

        public EdgeCertificates(string certificatePath, string keyPath, string trustedCertsPath)
        {
            Preconditions.CheckArgument(File.Exists(certificatePath));
            Preconditions.CheckArgument(File.Exists(keyPath));
            Preconditions.CheckArgument(File.Exists(trustedCertsPath));
            this.CertificatePath = certificatePath;
            this.KeyPath = keyPath;
            this.TrustedCertificatesPath = trustedCertsPath;
        }
    }
}
