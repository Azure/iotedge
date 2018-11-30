//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//----------------------------------------------------------------

namespace Microsoft.Azure.Devices.Edge.Hub.Http.Middleware
{
    using Microsoft.AspNetCore.Http;
    using System.Collections.Generic;
    using System.Security.Cryptography.X509Certificates;

    public static class ConnectionInfoExtensions
    {
        public static IList<X509Certificate2> GetClientCertificateChain(this ConnectionInfo connectionInfo, HttpContext context)
        {
            ITlsConnectionFeatureExtended feature = context.Features.Get<ITlsConnectionFeatureExtended>();
            return (feature == null) ? new List<X509Certificate2>() : feature.ChainElements;
        }
    }
}
