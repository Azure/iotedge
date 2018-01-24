// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.ProtocolGateway.Identity;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Persistence;
    using Microsoft.Extensions.Logging;
    using static System.FormattableString;

    public class SessionStateStoragePersistenceProvider : SessionStatePersistenceProvider
    {
        readonly IEntityStore<string, SessionState> sessionStore;

        public SessionStateStoragePersistenceProvider(IConnectionManager connectionManager, IEntityStore<string, SessionState> sessionStore)
            : base(connectionManager)
        {
            this.sessionStore = Preconditions.CheckNotNull(sessionStore, nameof(sessionStore));
            connectionManager.CloudConnectionEstablished += this.OnCloudConnectionEstablished;
        }

        async void OnCloudConnectionEstablished(object sender, IIdentity identity)
        {
            try
            {
                Preconditions.CheckNotNull(identity, nameof(identity));
                Events.SetSubscriptionsStarted(identity);
                Option<SessionState> sessionState = await this.sessionStore.Get(identity.Id);
                if (!sessionState.HasValue)
                {
                    Events.NoSessionStateFoundInStore(identity);
                }
                else
                {
                    await sessionState.ForEachAsync(
                        async s =>
                        {
                            await this.ProcessSessionSubscriptions(identity.Id, s);
                            Events.SetSubscriptionsSuccess(identity);
                        });
                }
            }
            catch (Exception ex)
            {
                Events.ClientReconnectError(ex, identity);
            }
        }

        public override async Task<ISessionState> GetAsync(IDeviceIdentity identity) => (await this.sessionStore.Get(identity.Id)).GetOrElse((SessionState)null);

        public override async Task SetAsync(IDeviceIdentity identity, ISessionState sessionState)
        {
            await base.SetAsync(identity, sessionState);
            await this.PersistToStore(identity.Id, sessionState);
            Events.SetSessionStateSuccess(identity);
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
                SetSubscriptionStarted = IdStart,
                ClientReconnectError,
                SetSubscriptionSuccess,
                SetSessionState,
                NoSessionState
            }

            public static void ClientReconnectError(Exception ex, IIdentity identity)
            {
                Log.LogWarning((int)EventIds.ClientReconnectError, ex, Invariant($"Error setting subscriptions for {identity.Id} on cloud reconnect"));
            }

            internal static void SetSubscriptionsSuccess(IIdentity identity)
            {
                Log.LogInformation((int)EventIds.SetSubscriptionSuccess, Invariant($"Set subscriptions from session state for {identity.Id} on cloud reconnect"));
            }

            internal static void SetSessionStateSuccess(IDeviceIdentity identity)
            {
                Log.LogInformation((int)EventIds.SetSessionState, Invariant($"Set subscriptions from session state for {identity.Id}"));
            }

            public static void SetSubscriptionsStarted(IIdentity identity)
            {
                Log.LogDebug((int)EventIds.SetSubscriptionStarted, Invariant($"Cloud connection established, setting subscriptions for {identity.Id}"));
            }

            public static void NoSessionStateFoundInStore(IIdentity identity)
            {
                Log.LogInformation((int)EventIds.NoSessionState, Invariant($"No session state found in store for {identity.Id}"));
            }
        }
    }
}
