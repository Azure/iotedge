// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http.Extensions
{
    using System.Collections.Generic;
    using System.Security.Cryptography.X509Certificates;

    public static class CertContext // TODO: Replace hacky POC
    {
        public static Dictionary<string, TlsConnectionFeatureExtended> CertsToChain = new Dictionary<string, TlsConnectionFeatureExtended>();
    }
}
