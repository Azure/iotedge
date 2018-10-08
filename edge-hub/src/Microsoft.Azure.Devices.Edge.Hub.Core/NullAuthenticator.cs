// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System.Threading.Tasks;

    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;

    public class NullAuthenticator : IAuthenticator
    {
        public Task<bool> AuthenticateAsync(IClientCredentials identity) => Task.FromResult(false);

        public Task<bool> ReauthenticateAsync(IClientCredentials identity) => Task.FromResult(false);
    }
}
