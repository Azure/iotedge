// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using System.Security.Cryptography.X509Certificates;

    public class Platform
    {
        public static readonly IPlatform Current = IsWindows() ? new Windows.Platform() as IPlatform : new Linux.Platform();

        public static bool IsWindows() => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

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
    }
}
