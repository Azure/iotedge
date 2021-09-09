// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;

    /// <summary>
    /// The <c>IAuthenticator</c> is responsible for authenticating a given device/module.
    /// An implementation could for instance, connect to Azure IoT Hub and open a connection
    /// to verify that the credentials supplied for the device/module is valid.
    /// </summary>
    public interface IAuthenticator
    {
        Task<bool> AuthenticateAsync(IClientCredentials identity);

        /// <summary>
        /// Reauthenticate does different things based on which method of authentication is being used
        /// If the authentication is local, it will do the full authentication.
        /// If authentication is cloud, then it will only check if the token is expired.
        /// </summary>
        Task<bool> ReauthenticateAsync(IClientCredentials identity);
    }
}
