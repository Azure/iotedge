// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class TokenCredentialsStore : ICredentialsStore
    {
        readonly IEntityStore<string, string> tokenStore;
        readonly IEncryptionProvider encryptionProvider;

        public TokenCredentialsStore(IEntityStore<string, string> tokenStore, IEncryptionProvider encryptionProvider)
        {
            this.tokenStore = Preconditions.CheckNotNull(tokenStore, nameof(tokenStore));
            this.encryptionProvider = Preconditions.CheckNotNull(encryptionProvider, nameof(encryptionProvider));
        }

        public async Task Add(IClientCredentials clientCredentials)
        {
            if (clientCredentials is ITokenCredentials tokenCredentials)
            {
                try
                {
                    string encryptedToken = await this.encryptionProvider.EncryptAsync(tokenCredentials.Token);
                    await this.tokenStore.Put(tokenCredentials.Identity.Id, encryptedToken);
                    Events.Stored(clientCredentials.Identity.Id);
                }
                catch (Exception e)
                {
                    Events.ErrorStoring(e, clientCredentials.Identity.Id);                    
                }
            }
        }

        public async Task<Option<IClientCredentials>> Get(IIdentity identity)
        {
            Option<string> tokenOption = await this.tokenStore.Get(identity.Id);
            return await tokenOption.Map(async encryptedToken =>
            {
                try
                {
                    string token = await this.encryptionProvider.DecryptAsync(encryptedToken);
                    Events.Retrieved(identity.Id);
                    return Option.Some(new TokenCredentials(identity, token, string.Empty) as IClientCredentials);
                }
                catch (Exception e)
                {
                    Events.ErrorGetting(e, identity.Id);
                    return Option.None<IClientCredentials>();
                }                
            })
            .GetOrElse(Task.FromResult(Option.None<IClientCredentials>()));
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<TokenCredentialsStore>();
            const int IdStart = HubCoreEventIds.TokenCredentialsStore;

            enum EventIds
            {
                ErrorStoring = IdStart,
                ErrorRetrieving,
                Retrieved,
                Stored
            }

            public static void Stored(string id)
            {
                Log.LogDebug((int)EventIds.Stored, $"Stored token for - {id}");
            }

            public static void ErrorStoring(Exception ex, string id)
            {
                Log.LogWarning((int)EventIds.ErrorStoring, ex, $"Error storing token for - {id}");
            }

            public static void Retrieved(string id)
            {
                Log.LogDebug((int)EventIds.Retrieved, $"Retrieved token for - {id}");
            }

            public static void ErrorGetting(Exception ex, string id)
            {
                Log.LogWarning((int)EventIds.ErrorRetrieving, ex, $"Error storing token for - {id}");
            }
        }
    }
}
