// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Mqtt.Subscription;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.ProtocolGateway.Identity;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Persistence;

    public class SessionStatePersistenceProvider : ISessionStatePersistenceProvider
    {
        readonly IConnectionManager connectionManager;
        readonly Option<IEntityStore<string, ISessionState>> sessionStore;

        public SessionStatePersistenceProvider(IConnectionManager connectionManager) : this(connectionManager, Option.None<IStoreProvider>()) { }

        public SessionStatePersistenceProvider(IConnectionManager connectionManager, Option<IStoreProvider> storeProvider)
        {
            this.connectionManager = Preconditions.CheckNotNull(connectionManager, nameof(connectionManager));
            Preconditions.CheckNotNull(storeProvider, nameof(storeProvider));
            this.sessionStore = storeProvider.Match(some => Option.Some(some.GetEntityStore<string, ISessionState>(Core.Constants.SessionStorePartitionKey)),
                () => Option.None<IEntityStore<string, ISessionState>>());
        }

        public ISessionState Create(bool transient)
        {
            return new SessionState(transient);
        }

        public Task<ISessionState> GetAsync(IDeviceIdentity identity)
        {
            return this.GetSessionFromStore(identity.Id);
        }

        public async Task SetAsync(IDeviceIdentity identity, ISessionState sessionState)
        {
            var registrationSessionState = sessionState as SessionState;
            if (registrationSessionState == null)
            {
                return;
            }

            IReadOnlyList<ISubscriptionRegistration> registrations = registrationSessionState.SubscriptionRegistrations;

            Option<ICloudProxy> cloudProxy = this.connectionManager.GetCloudConnection(identity.Id);
            foreach (ISubscriptionRegistration subscriptionRegistration in registrations)
            {
                await cloudProxy.Map(cp => subscriptionRegistration.ProcessSubscriptionAsync(cp)).GetOrElse(Task.FromResult(true));
            }

            registrationSessionState.ClearRegistrations();

            await this.PersistToStore(identity.Id, registrationSessionState);
        }

        public Task DeleteAsync(IDeviceIdentity identity, ISessionState sessionState)
        {
            return this.sessionStore.Match(some => some.Remove(identity.Id), () => Task.CompletedTask);
        }

        async Task<ISessionState> GetSessionFromStore(string id)
        {
            Option<ISessionState> sessionState = await this.sessionStore.Match(some => some.Get(id), () => Task.FromResult(Option.None<ISessionState>()));
            return sessionState.GetOrElse((ISessionState)null);
        }

        Task PersistToStore(string id, SessionState sessionState)
        {
            if (sessionState.ShouldSaveToStore)
            {
                return this.sessionStore.Match(some => some.PutOrUpdate(id, sessionState, u => sessionState), () => Task.CompletedTask);
            }
            else
            {
                return Task.CompletedTask;
            }
        }
    }
}
