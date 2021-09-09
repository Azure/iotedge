// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http
{
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Azure.Devices.Edge.Util;

    public interface IHttpProxiedCertificateExtractor
    {
        Task<Option<X509Certificate2>> GetClientCertificate(HttpContext context);
    }
}
