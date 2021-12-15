// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
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
        readonly DeviceSubscription[] allSubscriptions;

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

            // Later we need all possible subscriptions in use, but don't want to recalculate every time, so
            // store it now.
            this.allSubscriptions = this.subscriptionPatterns.Select(s => s.Subscription).Distinct().ToArray();
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

            if (subscriptionList == null)
            {
                // This case is valid and sent by the broker (as an empty string) when a client disconnects.
                // The meaning of the message is to remove subscriptions, but as the client is disconnecting
                // in a moment, we don't do anything. In fact, the disconnect message is supposed to arrive
                // first, and then this change notification gets ignored as it does not have related client.
                return true;
            }

            var deviceId = match.Groups["id1"].Success ? Option.Some<string>(match.Groups["id1"].Value) : Option.None<string>();
            var moduleId = match.Groups["id2"].Success ? Option.Some<string>(match.Groups["id2"].Value) : Option.None<string>();

            // For indirect clients the subscription change will be reported through edgeHub. That is a
            // slower method to reconcile so we handle the direct case separately with a faster method
            await moduleId.Filter(i => string.Equals(i, Constants.EdgeHubModuleId))
                     .Match(
                        _ => this.HandleNestedSubscriptionChanges(subscriptionList),
                        () => this.HandleDirectSubscriptionChanges(deviceId, moduleId, subscriptionList));

            return true;
        }

        async Task HandleDirectSubscriptionChanges(Option<string> deviceId, Option<string> moduleId, List<string> subscriptionList)
        {
            if (this.HasMixedIdentities(deviceId, moduleId, subscriptionList))
            {
                await this.HandleNestedSubscriptionChanges(subscriptionList);
                return;
            }

            var identity = moduleId.Match(
                               mod => deviceId.Match(
                                         dev => this.identityProvider.Create(dev, mod),
                                         () => throw new ArgumentException("Invalid topic structure for subscriptions - Module name matched but no Device name")),
                               () => deviceId.Match(
                                         dev => this.identityProvider.Create(dev),
                                         () => throw new ArgumentException("Invalid topic structure for subscriptions - no Device name matched")));

            var maybeListener = await this.connectionRegistry.GetOrCreateDeviceListenerAsync(identity, true);

            await maybeListener.Match(
                        async listener =>
                        {
                            foreach (var subscriptionPattern in this.subscriptionPatterns)
                            {
                                var subscribes = false;
                                foreach (var subscription in subscriptionList)
                                {
                                    if (this.TryMatchSubscription(subscriptionPattern, subscription, deviceId, moduleId))
                                    {
                                        subscribes = true;
                                        break;
                                    }
                                }

                                await AddOrRemoveSubscription(listener, subscribes, subscriptionPattern.Subscription);
                            }
                        },
                        () => Events.CouldNotObtainListener(identity.Id));
        }

        bool TryMatchSubscription(SubscriptionPattern subscriptionPattern, string subscription, Option<string> deviceId, Option<string> moduleId)
        {
            var subscriptionMatch = Regex.Match(subscription, subscriptionPattern.Pattern);
            if (subscriptionMatch.Success)
            {
                var subscribedDevId = subscriptionMatch.Groups["id1"].Success ? Option.Some<string>(subscriptionMatch.Groups["id1"].Value) : Option.None<string>();
                var subscribedModId = subscriptionMatch.Groups["id2"].Success ? Option.Some<string>(subscriptionMatch.Groups["id2"].Value) : Option.None<string>();

                if (IsMatchingIds(subscribedDevId, subscribedModId, deviceId, moduleId))
                {
                    return true;
                }
            }

            return false;
        }

        bool HasMixedIdentities(Option<string> deviceId, Option<string> moduleId, List<string> subscriptionList)
        {
            foreach (var subscriptionPattern in this.subscriptionPatterns)
            {
                foreach (var subscription in subscriptionList)
                {
                    var subscriptionMatch = Regex.Match(subscription, subscriptionPattern.Pattern);
                    if (subscriptionMatch.Success)
                    {
                        var subscribedDevId = subscriptionMatch.Groups["id1"].Success ? Option.Some<string>(subscriptionMatch.Groups["id1"].Value) : Option.None<string>();
                        var subscribedModId = subscriptionMatch.Groups["id2"].Success ? Option.Some<string>(subscriptionMatch.Groups["id2"].Value) : Option.None<string>();

                        if (!IsMatchingIds(subscribedDevId, subscribedModId, deviceId, moduleId))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        // A connection is nested when happens through a lower level edge device. In this case the
        // current level learns about the connections by 'sidechannels', e.g. when an nested client
        // subscribes to a topic. The notification of the subscription is assigned to $edgeHub and
        // and it contains the current subscription list of all nested-connected devices/modules.
        // There are two issues with this approach:
        //  1) it can happen that a subscription is sent by a device has never been seen before.
        //  2) if it is an unsubscribe, it can happen that no device/module related string can be
        //     found in the subscription list. E.g. if there is a device2 and the subscription list
        //     is ['device1/twin/res#'], then this notification also means that device2 is unsubscribed
        //     from all iothub related topics.
        // To solve 1), the connectionRegistry.GetOrCreateDeviceListenerAsync() creates the device if it has never
        // been seen before.
        // To solve 2), we need to take all known devices and unsubscribe from all topics not listed in the
        // current notification.
        async Task HandleNestedSubscriptionChanges(List<string> subscriptionList)
        {
            // As a first step, go through the sent subscription list and subscribe to everything it says.
            // The call returns all clients that had a subscription and ans also returns their subscriptions.
            // In the next step a second loop is going to unsubscribe from everything not in the list.
            var affectedClients = await this.SubscribeAndGetAffectedClients(subscriptionList);

            // At this point we have a collection about 'affectedClients'. Those are the clients subscribed to something.
            // What we don't know are the unsubscribtions. That is because the broker sends only the current subscription list,
            // so when a client unsubscribes, that is only a shorter list of subscriptions sent.
            // The information we want to gain is that what the unsubscriptions are, and the answer is that what we have not seen
            // previously as subscribed, that is unsubscribed. E.g. if a client subscribed to 'methods' only, then we say that
            // it unsubscribed from twin responses, c2d, m2m, etc. The underlaying layers take care of the optimization doing
            // nothing when unsubscribed twice from a topic.
            await this.UnsubscribeNotListedSubscriptions(affectedClients);
        }

        async Task<Dictionary<IIdentity, HashSet<DeviceSubscription>>> SubscribeAndGetAffectedClients(List<string> subscriptionList)
        {
            var affectedClients = new Dictionary<IIdentity, HashSet<DeviceSubscription>>();

            // We are trying to fit all possible subscription pattern to every item in the list
            foreach (var subscriptionPattern in this.subscriptionPatterns)
            {
                foreach (var subscription in subscriptionList)
                {
                    var subscriptionMatch = Regex.Match(subscription, subscriptionPattern.Pattern);
                    if (subscriptionMatch.Success)
                    {
                        // Once we have a hit (e.g. it is a method call subscription), we do two things:
                        // - subscribe to the given topic in the name of the client
                        // - store the fact that the subscription happened in the 'affectedClients' collection.
                        //   This collection will be used to decide about unsubscriptions later.
                        var deviceId = subscriptionMatch.Groups["id1"];
                        var moduleId = subscriptionMatch.Groups["id2"];

                        var identity = moduleId.Success
                                            ? this.identityProvider.Create(deviceId.Value, moduleId.Value)
                                            : this.identityProvider.Create(deviceId.Value);

                        var maybeListener = await this.connectionRegistry.GetOrCreateDeviceListenerAsync(identity, false);
                        await maybeListener.Match(
                            async listener =>
                            {
                                await AddOrRemoveSubscription(listener, true, subscriptionPattern.Subscription);

                                if (!affectedClients.TryGetValue(identity, out var subscriptions))
                                {
                                    subscriptions = new HashSet<DeviceSubscription>();
                                    affectedClients.Add(identity, subscriptions);
                                }

                                subscriptions.Add(subscriptionPattern.Subscription);
                            },
                            () => Events.CouldNotObtainListenerForSubscription(subscription.ToString(), identity.Id));
                    }
                }
            }

            return affectedClients;
        }

        async Task UnsubscribeNotListedSubscriptions(Dictionary<IIdentity, HashSet<DeviceSubscription>> affectedClients)
        {
            // As a first step we need all nested connections, because it is possible that affectedClients does not
            // cover all clients. It can happen when a client had only a single subscription before, but now it unsubscribed even from that.
            // In that case that client will not be contained by the subscription event sent by the broker, because it has no
            // subscription at all.
            var nestConnections = await this.connectionRegistry.GetNestedConnectionsAsync();
            foreach (var connection in nestConnections)
            {
                // for a given client we are going to unsubscribe from everything that was not marked as subscribed
                // from the broker subscription event. Every subscription marked in the broker event is stored in the
                // 'affectedClients' collection built previously.
                if (!affectedClients.TryGetValue(connection, out var subscriptions))
                {
                    subscriptions = new HashSet<DeviceSubscription>();
                }

                var maybeListener = await this.connectionRegistry.GetOrCreateDeviceListenerAsync(connection, false);

                foreach (var subscription in this.allSubscriptions)
                {
                    if (!subscriptions.Contains(subscription))
                    {
                        await maybeListener.Match(
                            listener => AddOrRemoveSubscription(listener, false, subscription),
                            () => Events.CouldNotObtainListenerForSubscription(subscription.ToString(), connection.Id));
                    }
                }
            }
        }

        static bool IsMatchingIds(Option<string> subscribedDevId, Option<string> subscribedModId, Option<string> deviceId, Option<string> moduleId)
        {
            // checking that both subscribedDevId and deviceId are defined and they have the same value
            var doesDevIdMatch = subscribedDevId.Match(
                                    sdi => deviceId.Match(
                                                di => string.Equals(sdi, di),
                                                () => false),
                                    () => false);

            // checking that both subscribedModId and moduleId are defined and they have the same value or both of them have no value (because it is a device not a module)
            var doesModIdMatch = subscribedModId.Match(
                                    smi => moduleId.Match(
                                                mi => string.Equals(smi, mi),
                                                () => false),
                                    () => false)
                              || (!subscribedModId.HasValue && !moduleId.HasValue);

            return doesDevIdMatch && doesModIdMatch;
        }

        static Task AddOrRemoveSubscription(IDeviceListener listener, bool add, DeviceSubscription subscription)
        {
            try
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
            catch (Exception e)
            {
                Events.FailedToChangeSubscriptionState(e, subscription.ToString(), listener.Identity.Id);
                return Task.CompletedTask;
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
                CouldNotObtainListenerForSubscription,
                CouldNotObtainListener,
                HandlingSubscriptionChange,
            }

            public static void BadPayloadFormat(Exception e) => Log.LogError((int)EventIds.BadPayloadFormat, e, "Bad payload format: cannot deserialize subscription update");
            public static void FailedToChangeSubscriptionState(Exception e, string subscription, string id) => Log.LogError((int)EventIds.FailedToChangeSubscriptionState, e, $"Failed to change subscrition status {subscription} for {id}");
            public static void HandlingSubscriptionChange(string content) => Log.LogDebug((int)EventIds.HandlingSubscriptionChange, $"Handling subscription change {content}");

            // These are called from Option.Match, where the Some() case is async - Task.CompletedTask is hidden here to make it look better at the match
            public static Task CouldNotObtainListenerForSubscription(string subscription, string id)
            {
                Log.LogError((int)EventIds.CouldNotObtainListenerForSubscription, $"Could not obtain DeviceListener to change subscrition status {subscription} for {id}");
                return Task.CompletedTask;
            }

            public static Task CouldNotObtainListener(string id)
            {
                Log.LogError((int)EventIds.CouldNotObtainListener, $"Could not obtain DeviceListener to change subscrition status for {id}");
                return Task.CompletedTask;
            }
        }
    }
}
