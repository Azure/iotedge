// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;

    public class TokenCredentialsStore : ICredentialsStore
    {
        readonly IEntityStore<string, string> tokenStore;        

        public TokenCredentialsStore(IEntityStore<string, string> tokenStore)
        {
            this.tokenStore = Preconditions.CheckNotNull(tokenStore, nameof(tokenStore));
        }

        public Task Add(IClientCredentials clientCredentials)
        {
            if (clientCredentials is ITokenCredentials tokenCredentials)
            {
                return this.tokenStore.Put(tokenCredentials.Identity.Id, tokenCredentials.Token);
            }
            return Task.CompletedTask;
        }

        public async Task<Option<IClientCredentials>> Get(IIdentity identity)
        {
            Option<string> tokenOption = await this.tokenStore.Get(identity.Id);
            return tokenOption.Map(token => new TokenCredentials(identity, token, string.Empty) as IClientCredentials);
        }
    }
}
