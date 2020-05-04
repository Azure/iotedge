// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http.Extensions
{
    using System;
    using System.Collections.Generic;
    using System.Security.Cryptography.X509Certificates;
    using Microsoft.AspNetCore.Http;

    public static class HttpContextExtensions
    {
        public static IList<X509Certificate2> GetClientCertificateChain(this HttpContext context)
        {
            var feature = context.Features.Get<ITlsConnectionFeatureExtended>();

            Console.WriteLine($"-------------CLIENT CERT CHAIN NULL CHECK: {feature == null}---------------");

            return (feature == null) ? new List<X509Certificate2>() : feature.ChainElements;
        }
    }
}
