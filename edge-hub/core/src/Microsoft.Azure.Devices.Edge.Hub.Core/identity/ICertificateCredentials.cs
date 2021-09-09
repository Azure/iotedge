// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Identity
{
    using System.Collections.Generic;
    using System.Security.Cryptography.X509Certificates;

    public interface ICertificateCredentials : IClientCredentials
    {
        X509Certificate2 ClientCertificate { get; }

        IList<X509Certificate2> ClientCertificateChain { get; }
    }
}
