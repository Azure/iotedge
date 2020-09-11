// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Identity
{
    using System.Collections.Generic;
    using System.Security.Cryptography.X509Certificates;
    using Microsoft.Azure.Devices.Edge.Util;

    public interface IClientCredentialsFactory
    {
        IClientCredentials GetWithX509Cert(string deviceId, string moduleId, string deviceClientType, X509Certificate2 clientCertificate, IList<X509Certificate2> clientChainCertificate, Option<string> modelId);

        IClientCredentials GetWithSasToken(string deviceId, string moduleId, string deviceClientType, string token, bool updatable, Option<string> modelId);

        IClientCredentials GetWithConnectionString(string connectionString);

        IClientCredentials GetWithIotEdged(string deviceId, string moduleId);
    }
}
