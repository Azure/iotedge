// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Certs
{
    public class LeafCertificates
    {
        public string CertificatePath { get; }

        public string KeyPath { get; }

        public LeafCertificates(string certificatePath, string keyPath)
        {
            this.CertificatePath = certificatePath;
            this.KeyPath = keyPath;
        }
    }
}
