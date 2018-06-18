// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;

    public class NullCredentialsStore : ICredentialsStore
    {
        public Task Add(IClientCredentials clientCredentials) => Task.CompletedTask;

        public Task<Option<IClientCredentials>> Get(IIdentity identity) => Task.FromResult(Option.None<IClientCredentials>());
    }
}
