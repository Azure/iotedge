// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    using Microsoft.Azure.Devices.Edge.Util;

    /// <summary>
    /// The <c>IdentityFactory</c> is responsible for creating <see cref="Identity"/> instances
    /// given device/module credentials. Implementations of this interface are expected to
    /// derive the right kind of identity instance (<see cref="DeviceIdentity"/> or <see cref="ModuleIdentity"/>)
    /// by examining the credentials.
    /// </summary>
    public interface IIdentityFactory
    {
        Try<Identity> GetWithSasToken(string username, string password);

        Try<Identity> GetWithHubKey(string username, string keyName, string keyValue);

        Try<Identity> GetWithDeviceKey(string username, string keyValue);
    }
}