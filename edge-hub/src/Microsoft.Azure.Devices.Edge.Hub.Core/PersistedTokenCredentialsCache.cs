// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    public class TokenCredentialsCache : ICredentialsCache
    {
        readonly IKeyValueStore<string, string> encryptedStore;

        public TokenCredentialsCache(IKeyValueStore<string, string> encryptedStore)
        {
            this.encryptedStore = Preconditions.CheckNotNull(encryptedStore, nameof(encryptedStore));
        }

        public async Task Add(IClientCredentials clientCredentials)
        {
            if (clientCredentials is ITokenCredentials tokenCredentials)
            {
                try
                {
                    var tokenCredentialsData = new TokenCredentialsData(tokenCredentials.Token, tokenCredentials.IsUpdatable);
                    await this.encryptedStore.Put(tokenCredentials.Identity.Id, tokenCredentialsData.ToJson());
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
            Option<string> tokenCredentialsDataOption = await this.encryptedStore.Get(identity.Id);
            return tokenCredentialsDataOption.Map(t =>
            {
                try
                {
                    var tokenCredentialsData = t.FromJson<TokenCredentialsData>();
                    Events.Retrieved(identity.Id);                    
                    return Option.Some(new TokenCredentials(identity, tokenCredentialsData.Token, string.Empty, tokenCredentialsData.IsUpdatable) as IClientCredentials);
                }
                catch (Exception e)
                {
                    Events.ErrorGetting(e, identity.Id);
                    return Option.None<IClientCredentials>();
                }                
            })
            .GetOrElse(Option.None<IClientCredentials>());
        }

        class TokenCredentialsData
        {
            [JsonConstructor]
            public TokenCredentialsData(string token, bool isUpdatable)
            {
                this.Token = token;
                this.IsUpdatable = isUpdatable;
            }

            [JsonProperty("isUpdatable")]
            public bool IsUpdatable { get; }

            [JsonProperty("token")]
            public string Token { get; }
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<TokenCredentialsCache>();
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
