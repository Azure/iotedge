// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.E2E.Test
{
    using System.Security.Cryptography.X509Certificates;

    static class ProtocolHeadFixtureCache
    {
        public static X509Certificate2 X509Certificate { get; set; }

        public static string EdgeDeviceConnectionString { get; set; }
    }
}
