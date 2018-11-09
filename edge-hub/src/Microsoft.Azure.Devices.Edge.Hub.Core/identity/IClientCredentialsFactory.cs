// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Identity
{
    public interface IClientCredentialsFactory
    {
        IClientCredentials GetWithConnectionString(string connectionString);

        IClientCredentials GetWithIotEdged(string deviceId, string moduleId);

        IClientCredentials GetWithSasToken(string deviceId, string moduleId, string deviceClientType, string token, bool updatable);

        IClientCredentials GetWithX509Cert(string deviceId, string moduleId, string deviceClientType);
    }
}
