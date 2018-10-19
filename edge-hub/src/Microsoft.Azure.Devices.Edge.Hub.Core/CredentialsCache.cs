// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;

    public class CredentialsCache : ICredentialsCache
    {
        readonly IDictionary<string, IClientCredentials> cache = new Dictionary<string, IClientCredentials>();
        readonly ICredentialsCache underlyingCache;
        readonly AsyncLock cacheLock = new AsyncLock();

        public CredentialsCache(ICredentialsCache underlyingCache)
        {
            this.underlyingCache = Preconditions.CheckNotNull(underlyingCache, nameof(underlyingCache));
        }

        public async Task Add(IClientCredentials clientCredentials)
        {
            using (await this.cacheLock.LockAsync())
            {
                this.cache[clientCredentials.Identity.Id] = clientCredentials;
                await this.underlyingCache.Add(clientCredentials);
            }
        }

        public async Task<Option<IClientCredentials>> Get(IIdentity identity)
        {
            using (await this.cacheLock.LockAsync())
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
}
