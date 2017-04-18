// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util.Test.Common
{
    using System.Security.Cryptography.X509Certificates;

    public static class CertificateHelper
    {
        public static X509Certificate2 GetCertificate(string thumbprint, 
            StoreName storeName, 
            StoreLocation storeLocation)
        {
            Preconditions.CheckNonWhiteSpace(thumbprint, nameof(thumbprint));
            var store = new X509Store(storeName, storeLocation);
            store.Open(OpenFlags.ReadOnly);
            X509Certificate2Collection col = store.Certificates.Find(X509FindType.FindByThumbprint, 
                thumbprint, 
                false);
            if (col != null && col.Count > 0)
            {
                return col[0];
            }
            return null;
        }
    }
}
