// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;

    public class CredentialsCache : ICredentialsCache
    {
        readonly IDictionary<string, IClientCredentials> cache = new ConcurrentDictionary<string, IClientCredentials>();
        readonly ICredentialsCache underlyingCache;

        public CredentialsCache(ICredentialsCache underlyingCache)
        {
            this.underlyingCache = Preconditions.CheckNotNull(underlyingCache, nameof(underlyingCache));
        }

        public Task Add(IClientCredentials clientCredentials)
        {
            this.cache.Add(clientCredentials.Identity.Id, clientCredentials);
            return this.underlyingCache.Add(clientCredentials);
        }

        public async Task<Option<IClientCredentials>> Get(IIdentity identity)
        {
            if (!this.cache.TryGetValue(identity.Id, out IClientCredentials clientCredentials))
            {
                Option<IClientCredentials> underlyingStoreCredentials = await this.underlyingCache.Get(identity);
                underlyingStoreCredentials.ForEach(c => this.cache.Add(identity.Id, c));
                return underlyingStoreCredentials;
            }

            return Option.Some(clientCredentials);
        }
    }
}
