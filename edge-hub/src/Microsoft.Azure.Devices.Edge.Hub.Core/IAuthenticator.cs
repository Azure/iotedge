// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System.Threading.Tasks;

    /// <summary>
    /// The <c>IAuthenticator</c> is responsible for authenticating a given device/module.
    /// An implementation could for instance, connect to Azure IoT Hub and open a connection
    /// to verify that the credentials supplied for the device/module is valid.
    /// </summary>
    public interface IAuthenticator
    {
        Task<bool> AuthenticateAsync(IIdentity identity);
    }
}
