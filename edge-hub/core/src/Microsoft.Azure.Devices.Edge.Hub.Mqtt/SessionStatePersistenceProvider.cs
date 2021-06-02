// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Persistence;
    using Microsoft.Extensions.Logging;
    using static System.FormattableString;
    using IDeviceIdentity = Microsoft.Azure.Devices.ProtocolGateway.Identity.IDeviceIdentity;

    public class SessionStatePersistenceProvider : ISessionStatePersistenceProvider
    {
        readonly IEdgeHub edgeHub;
        readonly AsyncLock setLock = new AsyncLock();

        public SessionStatePersistenceProvider(IEdgeHub edgeHub)
        {
            this.edgeHub = Preconditions.CheckNotNull(edgeHub, nameof(edgeHub));
        }

        public ISessionState Create(bool transient) => new SessionState(transient);

        public virtual Task<ISessionState> GetAsync(IDeviceIdentity identity)
        {
            // This class does not store the session state, so return null to Protocol gateway
            return Task.FromResult((ISessionState)null);
        }

        public virtual Task SetAsync(IDeviceIdentity identity, ISessionState sessionState) =>
            sessionState is SessionState registrationSessionState ? this.ProcessSessionSubscriptions(identity.Id, registrationSessionState) : Task.CompletedTask;

        public virtual Task DeleteAsync(IDeviceIdentity identity, ISessionState sessionState) => Task.CompletedTask;

        protected async Task ProcessSessionSubscriptions(string id, SessionState sessionState)
        {
            using (await this.setLock.LockAsync())
            {
                try
                {
                    IEnumerable<(DeviceSubscription, bool)> subscriptions = SessionStateParser.GetDeviceSubscriptions(sessionState.SubscriptionRegistrations, id);
                    Events.ProcessingSessionSubscriptions(id, subscriptions);

                    await this.edgeHub.ProcessSubscriptions(id, subscriptions);
                }
                catch (Exception ex)
                {
                    Events.ErrorProcessingSubscriptions(id, ex);
                }
            }

            // Don't clear subscriptions here. That way the subscriptions are set every time the connection
            // is re-established. Setting subscriptions is an idempotent operation.
        }

        static class Events
        {
            const int IdStart = MqttEventIds.SessionStatePersistenceProvider;
            static readonly ILogger Log = Logger.Factory.CreateLogger<SessionStatePersistenceProvider>();

            enum EventIds
            {
                ErrorHandlingSubscription = IdStart,
                ProcessingSessionSubscriptions
            }

            public static void ErrorProcessingSubscriptions(string id, Exception exception)
            {
                Log.LogWarning((int)EventIds.ErrorHandlingSubscription, exception, Invariant($"Error processing subscriptions for client {id}."));
            }

            internal static void ProcessingSessionSubscriptions(string id, IEnumerable<(DeviceSubscription, bool)> subscriptions)
            {
                string subscriptionString = string.Join(", ", subscriptions.Select(s => $"{s.Item1}"));
                Log.LogDebug((int)EventIds.ProcessingSessionSubscriptions, $"Processing session subscriptions {subscriptionString} for client {id}: ");
            }
        }
    }
}
