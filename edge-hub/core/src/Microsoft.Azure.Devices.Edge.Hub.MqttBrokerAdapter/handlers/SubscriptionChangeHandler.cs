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

            var id1 = match.Groups["id1"];
            var id2 = match.Groups["id2"];

            // For indirect clients the subscription change will be reported through edgeHub. That is a
            // slower method to reconcile so we handle the direct case separately with a faster method
            if (id2.Success && string.Equals(id2.Value, Constants.EdgeHubModuleId))
            {
                await this.HandleIndirectChanges(subscriptionList);
            }
            else
            {
                await this.HandleDirectChanges(id1, id2, subscriptionList);
            }

            return true;
        }

        async Task HandleDirectChanges(Group id1, Group id2, List<string> subscriptionList)
        {
            var identity = id2.Success
                                ? this.identityProvider.Create(id1.Value, id2.Value)
                                : this.identityProvider.Create(id1.Value);

            var listener = default(IDeviceListener);
            var maybeListener = await this.connectionRegistry.GetDeviceListenerAsync(identity, true);

            if (maybeListener.HasValue)
            {
                listener = maybeListener.Expect(() => new Exception($"No device listener found for {identity.Id}"));

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
                        await AddOrRemoveSubscription(listener, subscribes, subscriptionPattern.Subscription);
                    }
                    catch (Exception e)
                    {
                        Events.FailedToChangeSubscriptionState(e, subscriptionPattern.Subscription.ToString(), identity.Id);
                    }
                }
            }
            else
            {
                Events.CouldNotObtainListener(identity.Id);
            }
        }

        // A connection is indirect when happens through a lower level edge device. In this case the
        // current level learns about the connections by 'sidechannels', e.g. when an indirect client
        // subscribes to a topic. The notification of the subscription is assigned to $edgeHub and
        // and it contains the current subscription list of all indirectly connected devices/modules.
        // There are two issues with this approach:
        //  1) it can happen that a subscription is sent by a device has never been seen before.
        //  2) if it is an unsubscribe, it can happen that no device/module related string can be
        //     found in the subscription list. E.g. if there is a device2 and the subscription list
        //     is ['device1/twin/res#'], then this notification also means that device2 is unsubscribed
        //     from all iothub related topics.
        // To solve 1), the connectionRegistry.GetDeviceListenerAsync() creates the device if it has never
        // been seen before.
        // To solve 2), we need to take all known devices and unsubscribe from all topics not listed in the
        // current notification.
        async Task HandleIndirectChanges(List<string> subscriptionList)
        {
            // Collect all added subscriptions to this dictionary for every client. At the end we are going to
            // unsubscribe from everything not in this collection
            var affectedClients = new Dictionary<IIdentity, HashSet<DeviceSubscription>>();

            foreach (var subscriptionPattern in this.subscriptionPatterns)
            {
                foreach (var subscription in subscriptionList)
                {
                    var subscriptionMatch = Regex.Match(subscription, subscriptionPattern.Pattern);
                    if (subscriptionMatch.Success)
                    {
                        var id1 = subscriptionMatch.Groups["id1"];
                        var id2 = subscriptionMatch.Groups["id2"];

                        var identity = id2.Success
                                            ? this.identityProvider.Create(id1.Value, id2.Value)
                                            : this.identityProvider.Create(id1.Value);

                        var maybeListener = await this.connectionRegistry.GetDeviceListenerAsync(identity, false);
                        if (maybeListener.HasValue)
                        {
                            try
                            {
                                var listener = maybeListener.Expect(() => new Exception($"No device listener found for {identity.Id}"));
                                await AddOrRemoveSubscription(listener, true, subscriptionPattern.Subscription);

                                var subscriptions = default(HashSet<DeviceSubscription>);
                                if (!affectedClients.TryGetValue(identity, out subscriptions))
                                {
                                    subscriptions = new HashSet<DeviceSubscription>();
                                    affectedClients.Add(identity, subscriptions);
                                }

                                subscriptions.Add(subscriptionPattern.Subscription);
                            }
                            catch (Exception e)
                            {
                                Events.FailedToChangeSubscriptionState(e, subscriptionPattern.Subscription.ToString(), identity.Id);
                            }
                        }
                        else
                        {
                            Events.CouldNotObtainListenerForSubscription(subscriptionPattern.Subscription.ToString(), identity.Id);
                        }
                    }
                }
            }

            // The indirect connections list may contain clients not handled above. Those will unsubscribe from
            // everything
            var indirectConnections = await this.connectionRegistry.GetIndirectConnectionsAsync();

            foreach (var connection in indirectConnections)
            {
                var subscriptions = default(HashSet<DeviceSubscription>);
                if (!affectedClients.TryGetValue(connection, out subscriptions))
                {
                    subscriptions = new HashSet<DeviceSubscription>();
                }

                var maybeListener = await this.connectionRegistry.GetDeviceListenerAsync(connection, false);

                foreach (var subscription in this.allSubscriptions)
                {
                    if (!subscriptions.Contains(subscription))
                    {
                        if (maybeListener.HasValue)
                        {
                            try
                            {
                                var listener = maybeListener.Expect(() => new Exception($"No device listener found for {connection}"));
                                await AddOrRemoveSubscription(listener, false, subscription);
                            }
                            catch (Exception e)
                            {
                                Events.FailedToChangeSubscriptionState(e, subscription.ToString(), connection.Id);
                            }
                        }
                        else
                        {
                            Events.CouldNotObtainListenerForSubscription(subscription.ToString(), connection.Id);
                        }
                    }
                }
            }
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
                CouldNotObtainListenerForSubscription,
                CouldNotObtainListener,
                HandlingSubscriptionChange,
            }

            public static void BadPayloadFormat(Exception e) => Log.LogError((int)EventIds.BadPayloadFormat, e, "Bad payload format: cannot deserialize subscription update");
            public static void FailedToChangeSubscriptionState(Exception e, string subscription, string id) => Log.LogError((int)EventIds.FailedToChangeSubscriptionState, e, $"Failed to change subscrition status {subscription} for {id}");
            public static void CouldNotObtainListenerForSubscription(string subscription, string id) => Log.LogError((int)EventIds.CouldNotObtainListenerForSubscription, $"Could not obtain DeviceListener to change subscrition status {subscription} for {id}");
            public static void CouldNotObtainListener(string id) => Log.LogError((int)EventIds.CouldNotObtainListener, $"Could not obtain DeviceListener to change subscrition status for {id}");
            public static void HandlingSubscriptionChange(string content) => Log.LogDebug((int)EventIds.HandlingSubscriptionChange, $"Handling subscription change {content}");
        }
    }
}
