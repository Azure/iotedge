// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Http.Middleware
{
    using Microsoft.AspNetCore.Http;
    using System.Collections.Generic;
    using System.Security.Cryptography.X509Certificates;
    using Microsoft.Azure.Devices.Edge.Hub.Http.Extensions;

    public static class HttpContextExtensions
    {
        public static IList<X509Certificate2> GetClientCertificateChain(this HttpContext context)
        {
            ITlsConnectionFeatureExtended feature = context.Features.Get<ITlsConnectionFeatureExtended>();
            return (feature == null) ? new List<X509Certificate2>() : feature.ChainElements;
        }
    }
}
