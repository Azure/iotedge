// Copyright (c) Microsoft. All rights reserved.
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
