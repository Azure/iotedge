// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.ProtocolGateway.Identity;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Persistence;
    using Microsoft.Extensions.Logging;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using static System.FormattableString;

    public class SessionStateStoragePersistenceProvider : SessionStatePersistenceProvider
    {
        readonly IEntityStore<string, SessionState> sessionStore;
        readonly AsyncLock setLock = new AsyncLock();

        public SessionStateStoragePersistenceProvider(IEdgeHub edgeHub, IEntityStore<string, SessionState> sessionStore)
            : base(edgeHub)
        {
            this.sessionStore = Preconditions.CheckNotNull(sessionStore, nameof(sessionStore));
        }

        public override async Task<ISessionState> GetAsync(IDeviceIdentity identity) => (await this.sessionStore.Get(identity.Id)).GetOrElse((SessionState)null);

        public override async Task SetAsync(IDeviceIdentity identity, ISessionState sessionState)
        {
            using (await this.setLock.LockAsync())
            {
                await base.SetAsync(identity, sessionState);
                await this.PersistToStore(identity.Id, sessionState);
                Events.SetSessionStateSuccess(identity);
            }
        }

        public override Task DeleteAsync(IDeviceIdentity identity, ISessionState sessionState) => this.sessionStore.Remove(identity.Id);        

        Task PersistToStore(string id, ISessionState sessionState)
        {
            if (!(sessionState is SessionState registrationSessionState))
            {
                return Task.CompletedTask;
            }

            return registrationSessionState.ShouldSaveToStore
                ? this.sessionStore.Put(id, registrationSessionState)
                : Task.CompletedTask;

        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<SessionStateStoragePersistenceProvider>();
            const int IdStart = MqttEventIds.SessionStateStoragePersistenceProvider;

            enum EventIds
            {
                SetSessionState = IdStart
            }

            internal static void SetSessionStateSuccess(IDeviceIdentity identity)
            {
                Log.LogInformation((int)EventIds.SetSessionState, Invariant($"Set subscriptions from session state for {identity.Id}"));
            }
        }
    }
}
