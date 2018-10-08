// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System.Threading.Tasks;

    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;

    public class NullCredentialsCache : ICredentialsCache
    {
        public Task Add(IClientCredentials clientCredentials) => Task.CompletedTask;

        public Task<Option<IClientCredentials>> Get(IIdentity identity) => Task.FromResult(Option.None<IClientCredentials>());
    }
}
