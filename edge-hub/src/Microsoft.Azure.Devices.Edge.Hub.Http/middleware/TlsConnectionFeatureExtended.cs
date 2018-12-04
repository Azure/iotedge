// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Http.Middleware
{
    using System.Collections.Generic;
    using System.Security.Cryptography.X509Certificates;

    public class TlsConnectionFeatureExtended : ITlsConnectionFeatureExtended
    {
        public IList<X509Certificate2> ChainElements { get; set; }
    }
}
