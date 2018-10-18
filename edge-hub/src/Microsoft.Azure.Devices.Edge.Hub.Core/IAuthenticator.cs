// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
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
        /// <param name="identity">Client credentials</param>
        /// <returns>true if re-authentication succeeds, otherwise false.</returns>
        Task<bool> ReauthenticateAsync(IClientCredentials identity);
    }
}
