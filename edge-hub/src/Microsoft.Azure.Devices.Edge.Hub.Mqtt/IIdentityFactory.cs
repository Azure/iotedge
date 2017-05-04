// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    using Microsoft.Azure.Devices.Edge.Util;

    public interface IIdentityFactory
    {
        Try<Identity> GetWithSasToken(string username, string password);

        Try<Identity> GetWithHubKey(string username, string keyName, string keyValue);

        Try<Identity> GetWithDeviceKey(string username, string keyValue);
    }
}