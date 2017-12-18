// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.ProtocolGateway.Identity;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Persistence;
    using Microsoft.Extensions.Logging;
    using static System.FormattableString;

    public class SessionStatePersistenceProvider : ISessionStatePersistenceProvider
    {
        internal const string C2DSubscriptionTopicPrefix = @"messages/devicebound/#";
        internal const string MethodSubscriptionTopicPrefix = @"$iothub/methods/POST/";
        internal const string TwinSubscriptionTopicPrefix = @"$iothub/twin/PATCH/properties/desired/";
        internal const string TwinResponseTopicFilter = "$iothub/twin/res/#";
        static readonly Regex ModuleMessageTopicRegex = new Regex("^devices/.+/modules/.+/#$");

        readonly IConnectionManager connectionManager;

        public SessionStatePersistenceProvider(IConnectionManager connectionManager)
        {
            this.connectionManager = Preconditions.CheckNotNull(connectionManager, nameof(connectionManager));
        }

        public ISessionState Create(bool transient)
        {
            return new SessionState(transient);
        }

        public Task<ISessionState> GetAsync(IDeviceIdentity identity)
        {
            return Task.FromResult((ISessionState)null);
        }

        public Task SetAsync(IDeviceIdentity identity, ISessionState sessionState) =>
            sessionState is SessionState registrationSessionState ?
            this.ProcessSessionSubscriptions(identity.Id, registrationSessionState) :
            Task.CompletedTask;

        async Task ProcessSessionSubscriptions(string id, SessionState sessionState)
        {
            Option<ICloudProxy> cloudProxy = this.connectionManager.GetCloudConnection(id);
            await cloudProxy.ForEachAsync(async cp =>
            {
                foreach (KeyValuePair<string, bool> subscriptionRegistration in sessionState.SubscriptionRegistrations)
                {
                    string topicName = subscriptionRegistration.Key;
                    bool addSubscription = subscriptionRegistration.Value;

                    switch (GetSubscriptionTopic(topicName))
                    {
                        case SubscriptionTopic.Method:
                            if (addSubscription)
                            {
                                await cp.SetupCallMethodAsync();
                            }
                            else
                            {
                                await cp.RemoveCallMethodAsync();
                            }
                            break;

                        case SubscriptionTopic.TwinDesiredProperties:
                            if (addSubscription)
                            {
                                await cp.SetupDesiredPropertyUpdatesAsync();
                            }
                            else
                            {
                                await cp.RemoveDesiredPropertyUpdatesAsync();
                            }
                            break;

                        case SubscriptionTopic.C2D:
                            if (addSubscription)
                            {
                                cp.StartListening();
                            }
                            // No way to stop listening to C2D messages right now.
                            break;

                        case SubscriptionTopic.TwinResponse:
                            // No action required
                            break;

                        case SubscriptionTopic.ModuleMessage:
                            // No action required
                            break;

                        default:
                            Events.UnknownTopicSubscription(topicName);
                            break;
                    }
                }

                sessionState.ClearRegistrations();
            });
        }


        public Task DeleteAsync(IDeviceIdentity identity, ISessionState sessionState) => Task.CompletedTask;

        internal static SubscriptionTopic GetSubscriptionTopic(string topicName)
        {
            if (topicName.StartsWith(MethodSubscriptionTopicPrefix))
            {
                return SubscriptionTopic.Method;
            }
            else if (topicName.StartsWith(TwinSubscriptionTopicPrefix))
            {
                return SubscriptionTopic.TwinDesiredProperties;
            }
            else if (topicName.EndsWith(C2DSubscriptionTopicPrefix))
            {
                return SubscriptionTopic.C2D;
            }
            else if (topicName.Equals(TwinResponseTopicFilter))
            {
                return SubscriptionTopic.TwinResponse;
            }
            else if (ModuleMessageTopicRegex.IsMatch(topicName))
            {
                return SubscriptionTopic.ModuleMessage;
            }
            else
            {
                return SubscriptionTopic.Unknown;
            }
        }

        internal enum SubscriptionTopic
        {
            Method,
            TwinDesiredProperties,
            C2D,
            TwinResponse,
            ModuleMessage,
            Unknown
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<SessionStatePersistenceProvider>();
            const int IdStart = MqttEventIds.SessionStatePersistenceProvider;

            enum EventIds
            {
                UnknownSubscription = IdStart
            }

            public static void UnknownTopicSubscription(string topicName)
            {
                Log.LogInformation((int)EventIds.UnknownSubscription, Invariant($"Ignoring unknown subscription to topic {topicName}."));
            }
        }
    }
}
