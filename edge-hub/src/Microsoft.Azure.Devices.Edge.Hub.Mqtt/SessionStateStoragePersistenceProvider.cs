// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.ProtocolGateway.Identity;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Persistence;

    public class SessionStateStoragePersistenceProvider : ISessionStatePersistenceProvider
    {
        readonly IEntityStore<string, ISessionState> sessionStore;
        readonly SessionStatePersistenceProvider sessionStatePersistenceProvider;

        public SessionStateStoragePersistenceProvider(IConnectionManager connectionManager, IEntityStore<string, ISessionState> sessionStore)
        {
            this.sessionStore = Preconditions.CheckNotNull(sessionStore, nameof(sessionStore));
            this.sessionStatePersistenceProvider = new SessionStatePersistenceProvider(Preconditions.CheckNotNull(connectionManager, nameof(connectionManager)));
        }

        public ISessionState Create(bool transient)
        {
            return this.sessionStatePersistenceProvider.Create(transient);
        }

        public Task<ISessionState> GetAsync(IDeviceIdentity identity)
        {
            return this.GetSessionFromStore(identity.Id);
        }

        public async Task SetAsync(IDeviceIdentity identity, ISessionState sessionState)
        {
            await this.sessionStatePersistenceProvider.SetAsync(identity, sessionState);
            await this.PersistToStore(identity.Id, sessionState);
        }

        public Task DeleteAsync(IDeviceIdentity identity, ISessionState sessionState)
        {
            return this.sessionStore.Remove(identity.Id);
        }

        async Task<ISessionState> GetSessionFromStore(string id)
        {
            Option<ISessionState> sessionState = await this.sessionStore.Get(id);
            return sessionState.GetOrElse((ISessionState)null);
        }

        Task PersistToStore(string id, ISessionState sessionState)
        {
            var registrationSessionState = sessionState as SessionState;
            if (registrationSessionState == null)
            {
                return Task.CompletedTask;
            }

            return registrationSessionState.ShouldSaveToStore ? this.sessionStore.PutOrUpdate(id, sessionState, u => sessionState) : Task.CompletedTask;
        }
    }
}
