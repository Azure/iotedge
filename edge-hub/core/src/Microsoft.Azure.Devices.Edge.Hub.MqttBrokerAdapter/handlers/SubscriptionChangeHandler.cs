// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    public class SubscriptionChangeHandler : IMessageConsumer
    {
        const string SubscriptionChangePattern = @"^\$edgehub/(?<id1>[^/\+\#]+)(/(?<id2>[^/\+\#]+))?/subscriptions$";

        const string SubscriptionChangeDevice = "$edgehub/+/subscriptions";
        const string SubscriptionChangeModule = "$edgehub/+/+/subscriptions";

        static readonly string[] subscriptions = new[] { SubscriptionChangeDevice, SubscriptionChangeModule };

        readonly IConnectionRegistry connectionRegistry;
        readonly IIdentityProvider identityProvider;
        readonly List<SubscriptionPattern> subscriptionPatterns;

        public IReadOnlyCollection<string> Subscriptions => subscriptions;

        public SubscriptionChangeHandler(
                    ICloud2DeviceMessageHandler cloud2DeviceMessageHandler,
                    IModuleToModuleMessageHandler moduleToModuleMessageHandler,
                    IDirectMethodHandler directMethodHandler,
                    ITwinHandler twinHandler,
                    IConnectionRegistry connectionRegistry,
                    IIdentityProvider identityProvider)
        {
            Preconditions.CheckNotNull(cloud2DeviceMessageHandler);
            Preconditions.CheckNotNull(moduleToModuleMessageHandler);
            Preconditions.CheckNotNull(directMethodHandler);
            Preconditions.CheckNotNull(twinHandler);

            this.connectionRegistry = Preconditions.CheckNotNull(connectionRegistry);
            this.identityProvider = Preconditions.CheckNotNull(identityProvider);

            this.subscriptionPatterns = new List<SubscriptionPattern>();
            this.subscriptionPatterns.AddRange(Preconditions.CheckNotNull(cloud2DeviceMessageHandler.WatchedSubscriptions));
            this.subscriptionPatterns.AddRange(Preconditions.CheckNotNull(moduleToModuleMessageHandler.WatchedSubscriptions));
            this.subscriptionPatterns.AddRange(Preconditions.CheckNotNull(directMethodHandler.WatchedSubscriptions));
            this.subscriptionPatterns.AddRange(Preconditions.CheckNotNull(twinHandler.WatchedSubscriptions));
        }

        public async Task<bool> HandleAsync(MqttPublishInfo publishInfo)
        {
            var match = Regex.Match(publishInfo.Topic, SubscriptionChangePattern);

            if (!match.Success)
            {
                return false;
            }

            // After this point we know that this was a subscription change and will return 'true'
            // even in a case of error
            var subscriptionList = default(List<string>);
            try
            {
                var payloadAsString = Encoding.UTF8.GetString(publishInfo.Payload);
                subscriptionList = JsonConvert.DeserializeObject<List<string>>(payloadAsString);

                Events.HandlingSubscriptionChange(payloadAsString);
            }
            catch (Exception e)
            {
                Events.BadPayloadFormat(e);
                return true;
            }

            var id1 = match.Groups["id1"];
            var id2 = match.Groups["id2"];

            var identity = id2.Success
                                ? this.identityProvider.Create(id1.Value, id2.Value)
                                : this.identityProvider.Create(id1.Value);

            var listener = default(IDeviceListener);
            var maybeListener = await this.connectionRegistry.GetDeviceListenerAsync(identity);

            if (!maybeListener.HasValue)
            {
                return true;
            }
            else
            {
                listener = maybeListener.Expect(() => new Exception($"No device listener found for {identity.Id}"));
            }

            foreach (var subscriptionPattern in this.subscriptionPatterns)
            {
                var subscribes = false;

                foreach (var subscription in subscriptionList)
                {
                    var subscriptionMatch = Regex.Match(subscription, subscriptionPattern.Pattern);
                    if (IsMatchWithIds(subscriptionMatch, id1, id2))
                    {
                        subscribes = true;
                        break;
                    }
                }

                try
                {
                    await AddOrRemoveSubscription(listener, subscribes, subscriptionPattern.Subscrition);
                }
                catch (Exception e)
                {
                    Events.FailedToChangeSubscriptionState(e, subscriptionPattern.Subscrition.ToString(), identity.Id);
                }
            }

            return true;
        }

        static bool IsMatchWithIds(Match match, Group id1, Group id2)
        {
            if (match.Success)
            {
                var subscriptionId1 = match.Groups["id1"];
                var subscriptionId2 = match.Groups["id2"];

                var id1Match = id1.Success && subscriptionId1.Success && id1.Value == subscriptionId1.Value;
                var id2Match = (id2.Success && subscriptionId2.Success && id2.Value == subscriptionId2.Value) || (!id2.Success && !subscriptionId2.Success);

                return id1Match && id2Match;
            }

            return false;
        }

        static Task AddOrRemoveSubscription(IDeviceListener listener, bool add, DeviceSubscription subscription)
        {
            if (add)
            {
                return listener.AddSubscription(subscription);
            }
            else
            {
                return listener.RemoveSubscription(subscription);
            }
        }

        static class Events
        {
            const int IdStart = MqttBridgeEventIds.SubscriptionChangeHandler;
            static readonly ILogger Log = Logger.Factory.CreateLogger<SubscriptionChangeHandler>();

            enum EventIds
            {
                BadPayloadFormat = IdStart,
                FailedToChangeSubscriptionState,
                HandlingSubscriptionChange,
            }

            public static void BadPayloadFormat(Exception e) => Log.LogError((int)EventIds.BadPayloadFormat, e, "Bad payload format: cannot deserialize subscription update");
            public static void FailedToChangeSubscriptionState(Exception e, string subscription, string id) => Log.LogError((int)EventIds.FailedToChangeSubscriptionState, e, $"Failed to change subscrition status {subscription} for {id}");
            public static void HandlingSubscriptionChange(string content) => Log.LogDebug((int)EventIds.HandlingSubscriptionChange, $"Handling subscription change {content}");
        }
    }
}
