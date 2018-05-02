// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core.Identity
{
    public interface IClientCredentialsFactory
    {
        IClientCredentials GetWithX509Cert(string deviceId, string moduleId, string deviceClientType);

        IClientCredentials GetWithSasToken(string deviceId, string moduleId, string deviceClientType, string token);

        IClientCredentials GetWithConnectionString(string connectionString);

        IClientCredentials GetWithIotEdged(string deviceId, string moduleId);
    }
}
