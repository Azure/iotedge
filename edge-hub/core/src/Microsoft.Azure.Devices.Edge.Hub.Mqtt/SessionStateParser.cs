// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    using static System.FormattableString;

    public class SessionStateParser
    {
        internal const string C2DSubscriptionTopicPrefix = @"messages/devicebound/#";
        internal const string MethodSubscriptionTopicPrefix = @"$iothub/methods/POST/";
        internal const string TwinSubscriptionTopicPrefix = @"$iothub/twin/PATCH/properties/desired/";
        internal const string TwinResponseTopicFilter = "$iothub/twin/res/#";
        internal static readonly Regex ModuleMessageTopicRegex = new Regex("^devices/.+/modules/.+/#$");

        public static IEnumerable<(DeviceSubscription, bool)> GetDeviceSubscriptions(IReadOnlyDictionary<string, bool> topics, string id)
        {
            return topics.Select(
                            subscriptionRegistration =>
                            {
                                string topicName = subscriptionRegistration.Key;
                                bool addSubscription = subscriptionRegistration.Value;
                                DeviceSubscription deviceSubscription = GetDeviceSubscription(topicName);
                                if (deviceSubscription == DeviceSubscription.Unknown)
                                {
                                    Events.UnknownTopicSubscription(topicName, id);
                                }

                                return (deviceSubscription, addSubscription);
                            });
        }

        public static DeviceSubscription GetDeviceSubscription(string topicName)
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
            const int IdStart = MqttEventIds.SessionStateParser;
            static readonly ILogger Log = Logger.Factory.CreateLogger<SessionStateParser>();

            enum EventIds
            {
                UnknownSubscription = IdStart,
            }

            public static void UnknownTopicSubscription(string topicName, string id)
            {
                Log.LogInformation((int)EventIds.UnknownSubscription, Invariant($"Ignoring unknown subscription to topic {topicName} for client {id}."));
            }
        }
    }
}
