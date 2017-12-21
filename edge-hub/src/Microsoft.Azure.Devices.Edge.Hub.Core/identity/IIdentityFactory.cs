// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core.Identity
{
    using Microsoft.Azure.Devices.Edge.Util;

    public interface IIdentityFactory
    {
        Try<IIdentity> GetWithSasToken(
            string deviceId,
            string moduleId,
            string deviceClientType,
            bool isModuleIdentity,
            string token);

        Try<IIdentity> GetWithConnectionString(string connectionString);

        Try<IIdentity> GetWithHubKey(
            string deviceId,
            string moduleId,
            string deviceClientType,
            bool isModuleIdentity,
            string keyName,
            string keyValue);

        Try<IIdentity> GetWithDeviceKey(
            string deviceId,
            string moduleId,
            string deviceClientType,
            bool isModuleIdentity,
            string keyValue);
    }
}
