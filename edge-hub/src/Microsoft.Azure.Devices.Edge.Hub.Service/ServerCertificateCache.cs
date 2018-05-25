// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Service
{
    using System.Security.Cryptography.X509Certificates;

    internal static class ServerCertificateCache
    {
        public static X509Certificate2 X509Certificate { get; set; }
    }
}
