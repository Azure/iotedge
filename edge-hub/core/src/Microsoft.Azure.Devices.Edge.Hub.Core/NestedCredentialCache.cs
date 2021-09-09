// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;

    public class NestedCredentialsCache : ICredentialsCache
    {
        // Maps client identity -> credential used for auth
        readonly IDictionary<string, IClientCredentials> credentials = new Dictionary<string, IClientCredentials>();

        // Maps OnBehalfOf actor device -> children, e.g. edge1 -> [leaf1, edge1/$edgeAgent, ...]
        readonly IDictionary<string, HashSet<string>> onBehalfOfRelations = new Dictionary<string, HashSet<string>>();

        readonly ICredentialsCache underlyingCache;
        readonly AsyncLock cacheLock = new AsyncLock();

        public NestedCredentialsCache(ICredentialsCache underlyingCache)
        {
            this.underlyingCache = Preconditions.CheckNotNull(underlyingCache, nameof(underlyingCache));
        }

        public async Task Add(IClientCredentials clientCredentials)
        {
            using (await this.cacheLock.LockAsync())
            {
                // Update the in-memory relations
                this.UpdateCachedCredentials(clientCredentials);

                // Update the DB
                await this.underlyingCache.Add(clientCredentials);
            }
        }

        public async Task<Option<IClientCredentials>> Get(IIdentity identity)
        {
            using (await this.cacheLock.LockAsync())
            {
                if (!this.credentials.TryGetValue(identity.Id, out IClientCredentials clientCredentials))
                {
                    // Update the in-memory relations as we read credentials out of the DB
                    Option<IClientCredentials> underlyingStoreCredentials = await this.underlyingCache.Get(identity);
                    underlyingStoreCredentials.ForEach(creds => this.UpdateCachedCredentials(creds));
                    return underlyingStoreCredentials;
                }

                return Option.Some(clientCredentials);
            }
        }

        void UpdateCachedCredentials(IClientCredentials clientCredentials)
        {
            string targetId = AuthChainHelpers.GetAuthTarget(clientCredentials.AuthChain).GetOrElse(clientCredentials.Identity.Id);

            if (clientCredentials.AuthChain.HasValue)
            {
                // OnBehalfOf connection from a child Edge, there should always be an actor device
                string actorDeviceId = AuthChainHelpers.GetActorDeviceId(clientCredentials.AuthChain)
                    .Expect(() => new ArgumentException($"Invalid auth-chain: {clientCredentials.AuthChain.OrDefault()}"));

                // Only EdgeHub can act OnBehalfOf another identity
                string actorId = $"{actorDeviceId}/{Constants.EdgeHubModuleId}";

                if (!this.onBehalfOfRelations.ContainsKey(actorId))
                {
                    // Create a new OnBehalfOf mapping for this actor Edge
                    this.onBehalfOfRelations[actorId] = new HashSet<string>();
                }

                // Update our books for the new relation
                this.onBehalfOfRelations[actorId].Add(targetId);
            }
            else
            {
                // If there's no auth-chain, then this could be a credential update
                // for a child EdgeHub acting OnBehalfOf other identities
                if (this.onBehalfOfRelations.TryGetValue(clientCredentials.Identity.Id, out HashSet<string> onBehalfOfClients))
                {
                    // Need to update all clients that the child EdgeHub is acting OnBehalfOf
                    foreach (string client in onBehalfOfClients)
                    {
                        IClientCredentials updatedCredentials = GetCredentialsWithOnBehalfOfUpdates(this.credentials[client], clientCredentials);
                        this.credentials[client] = updatedCredentials;
                    }
                }
            }

            // Update the client credentials
            this.credentials[targetId] = clientCredentials;
        }

        public static IClientCredentials GetCredentialsWithOnBehalfOfUpdates(IClientCredentials originalClientCreds, IClientCredentials updatedActorCreds)
        {
            if (originalClientCreds is ITokenCredentials && updatedActorCreds is ITokenCredentials)
            {
                // When a child EdgeHub has its credentials updated, every client it's
                // acting OnBehalfOf also need to have their credentials updated. The
                // result is to copy the identity and token from the new EdgeHub credentials,
                // while keeping everything else from the original client credentials.
                var updatedToken = updatedActorCreds as ITokenCredentials;
                return new TokenCredentials(updatedToken.Identity, updatedToken.Token, originalClientCreds.ProductInfo, originalClientCreds.ModelId, originalClientCreds.AuthChain, updatedToken.IsUpdatable);
            }
            else
            {
                // Only token credentials can have OnBehalfOf updates
                throw new ArgumentException($"Credentials type mismatch, original: {originalClientCreds.GetType()}, updated: {updatedActorCreds.GetType()}");
            }
        }
    }
}
