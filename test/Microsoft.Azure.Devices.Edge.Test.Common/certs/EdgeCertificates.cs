// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Certs
{
    using System;
    using System.Threading.Tasks;

    public class EdgeCertificates
    {
        public EdgeCertificates(string certificatePath, string keyPath, string trustedCertsPath)
        {
            this.CertificatePath = certificatePath;
            this.KeyPath = keyPath;
            this.TrustedCertsPath = trustedCertsPath;
        }

        public string CertificatePath { get; }

        public string KeyPath { get; }

        public string TrustedCertsPath { get; }
    }
}
