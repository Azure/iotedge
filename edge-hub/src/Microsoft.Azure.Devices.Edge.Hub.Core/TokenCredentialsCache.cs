// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.Threading.Tasks;

    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

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
                    await this.encryptedStore.Put(tokenCredentials.Identity.Id, tokenCredentials.Token);
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
            Option<string> tokenOption = await this.encryptedStore.Get(identity.Id);
            return tokenOption.Map(
                    token =>
                    {
                        try
                        {
                            Events.Retrieved(identity.Id);
                            return Option.Some(new TokenCredentials(identity, token, string.Empty) as IClientCredentials);
                        }
                        catch (Exception e)
                        {
                            Events.ErrorGetting(e, identity.Id);
                            return Option.None<IClientCredentials>();
                        }
                    })
                .GetOrElse(Option.None<IClientCredentials>());
        }

        static class Events
        {
            const int IdStart = HubCoreEventIds.TokenCredentialsStore;

            static readonly ILogger Log = Logger.Factory.CreateLogger<TokenCredentialsCache>();

            enum EventIds
            {
                ErrorStoring = IdStart,

                ErrorRetrieving,

                Retrieved,

                Stored
            }

            public static void ErrorGetting(Exception ex, string id)
            {
                Log.LogWarning((int)EventIds.ErrorRetrieving, ex, $"Error storing token for - {id}");
            }

            public static void ErrorStoring(Exception ex, string id)
            {
                Log.LogWarning((int)EventIds.ErrorStoring, ex, $"Error storing token for - {id}");
            }

            public static void Retrieved(string id)
            {
                Log.LogDebug((int)EventIds.Retrieved, $"Retrieved token for - {id}");
            }

            public static void Stored(string id)
            {
                Log.LogDebug((int)EventIds.Stored, $"Stored token for - {id}");
            }
        }
    }
}
