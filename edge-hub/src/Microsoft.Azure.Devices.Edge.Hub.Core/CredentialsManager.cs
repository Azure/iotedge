// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;

    public class CredentialsManager : ICredentialsStore
    {
        readonly IDictionary<string, IClientCredentials> cache = new ConcurrentDictionary<string, IClientCredentials>();
        readonly ICredentialsStore underlyingStore;

        public CredentialsManager(ICredentialsStore underlyingStore)
        {
            this.underlyingStore = Preconditions.CheckNotNull(underlyingStore, nameof(underlyingStore));
        }

        public Task Add(IClientCredentials clientCredentials)
        {
            this.cache.Add(clientCredentials.Identity.Id, clientCredentials);
            return this.underlyingStore.Add(clientCredentials);
        }

        public async Task<Option<IClientCredentials>> Get(IIdentity identity)
        {
            if (!this.cache.TryGetValue(identity.Id, out IClientCredentials clientCredentials))
            {
                Option<IClientCredentials> underlyingStoreCredentials = await this.underlyingStore.Get(identity);
                underlyingStoreCredentials.ForEach(c => this.cache.Add(identity.Id, c));
                return underlyingStoreCredentials;
            }

            return Option.Some(clientCredentials);
        }
    }
}
