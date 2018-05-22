// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Persistence;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using static System.FormattableString;
    using IDeviceIdentity = Microsoft.Azure.Devices.ProtocolGateway.Identity.IDeviceIdentity;

    public class SessionStatePersistenceProvider : ISessionStatePersistenceProvider
    {
        internal const string C2DSubscriptionTopicPrefix = @"messages/devicebound/#";
        internal const string MethodSubscriptionTopicPrefix = @"$iothub/methods/POST/";
        internal const string TwinSubscriptionTopicPrefix = @"$iothub/twin/PATCH/properties/desired/";
        internal const string TwinResponseTopicFilter = "$iothub/twin/res/#";
        static readonly Regex ModuleMessageTopicRegex = new Regex("^devices/.+/modules/.+/#$");

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
            sessionState is SessionState registrationSessionState ?
            this.ProcessSessionSubscriptions(identity.Id, registrationSessionState) :
            Task.CompletedTask;

        protected async Task ProcessSessionSubscriptions(string id, SessionState sessionState)
        {
            using (await this.setLock.LockAsync())
            {
                foreach (KeyValuePair<string, bool> subscriptionRegistration in sessionState.SubscriptionRegistrations)
                {
                    string topicName = subscriptionRegistration.Key;
                    bool addSubscription = subscriptionRegistration.Value;

                    try
                    {
                        Events.ProcessingSubscription(id, topicName, addSubscription);
                        DeviceSubscription deviceSubscription = GetDeviceSubscription(topicName);
                        if (deviceSubscription == DeviceSubscription.Unknown)
                        {
                            Events.UnknownTopicSubscription(topicName, id);
                        }
                        else
                        {
                            if (addSubscription)
                            {
                                await this.edgeHub.AddSubscription(id, deviceSubscription);
                            }
                            else
                            {
                                await this.edgeHub.RemoveSubscription(id, deviceSubscription);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Events.ErrorHandlingSubscription(id, topicName, addSubscription, ex);
                    }
                }
            }
            // Don't clear subscriptions here. That way the subscriptions are set every time the connection
            // is re-established. Setting subscriptions is an idempotent operation.             
        }

        public virtual Task DeleteAsync(IDeviceIdentity identity, ISessionState sessionState) => Task.CompletedTask;

        internal static DeviceSubscription GetDeviceSubscription(string topicName)
        {
            Preconditions.CheckNonWhiteSpace(topicName, nameof(topicName));
            if (topicName.StartsWith(MethodSubscriptionTopicPrefix))
            {
                return DeviceSubscription.Methods;
            }
            else if (topicName.StartsWith(TwinSubscriptionTopicPrefix))
            {
                return DeviceSubscription.DesiredPropertyUpdates;
            }
            else if (topicName.EndsWith(C2DSubscriptionTopicPrefix))
            {
                return DeviceSubscription.C2D;
            }
            else if (topicName.Equals(TwinResponseTopicFilter))
            {
                return DeviceSubscription.TwinResponse;
            }
            else if (ModuleMessageTopicRegex.IsMatch(topicName))
            {
                return DeviceSubscription.ModuleMessages;
            }
            else
            {
                return DeviceSubscription.Unknown;
            }
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<SessionStatePersistenceProvider>();
            const int IdStart = MqttEventIds.SessionStatePersistenceProvider;

            enum EventIds
            {
                UnknownSubscription = IdStart,
                ErrorHandlingSubscription
            }

            public static void UnknownTopicSubscription(string topicName, string id)
            {
                Log.LogInformation((int)EventIds.UnknownSubscription, Invariant($"Ignoring unknown subscription to topic {topicName} for client {id}."));
            }

            public static void ErrorHandlingSubscription(string id, string topicName, bool addSubscription, Exception exception)
            {
                string action = addSubscription ? "adding" : "removing";
                Log.LogWarning((int)EventIds.ErrorHandlingSubscription, exception, Invariant($"Error {action} subscription {topicName} for client {id}."));
            }

            public static void ProcessingSubscription(string id, string topicName, bool addSubscription)
            {
                string action = addSubscription ? "Adding" : "Removing";
                Log.LogDebug((int)EventIds.ErrorHandlingSubscription, Invariant($"{action} subscription {topicName} for client {id}."));
            }
        }
    }
}
