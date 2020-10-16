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

            if (subscriptionList == null)
            {
                // This case is valid and sent by the broker (as an empty string) when a client disconnects.
                // The meaning of the message is to remove subscriptions, but as the client is disconnecting
                // in a moment, we don't do anything. In fact, the disconnect message is supposed to arrive
                // first, and then this change notification gets ignored as it does not have related client.
                return true;
            }

            // TODO: the following solution is to unblock the case when in a nested scenario a child edgehub
            // subscribes in the name of a device/module. However changes needed to support unsubscribe
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

                        var maybeListener = await this.connectionRegistry.GetDeviceListenerAsync(identity);
                        if (maybeListener.HasValue)
                        {
                            try
                            {
                                var listener = maybeListener.Expect(() => new Exception($"No device listener found for {identity.Id}"));
                                await AddOrRemoveSubscription(listener, true, subscriptionPattern.Subscrition);
                            }
                            catch (Exception e)
                            {
                                Events.FailedToChangeSubscriptionState(e, subscriptionPattern.Subscrition.ToString(), identity.Id);
                            }
                        }
                        else
                        {
                            Events.CouldNotObtainListener(subscriptionPattern.Subscrition.ToString(), identity.Id);
                        }

                        break;
                    }
                }
            }

            return true;
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
                CouldNotObtainListener,
                HandlingSubscriptionChange,
            }

            public static void BadPayloadFormat(Exception e) => Log.LogError((int)EventIds.BadPayloadFormat, e, "Bad payload format: cannot deserialize subscription update");
            public static void FailedToChangeSubscriptionState(Exception e, string subscription, string id) => Log.LogError((int)EventIds.FailedToChangeSubscriptionState, e, $"Failed to change subscrition status {subscription} for {id}");
            public static void CouldNotObtainListener(string subscription, string id) => Log.LogError((int)EventIds.CouldNotObtainListener, $"Could not obtain DeviceListener to change subscrition status {subscription} for {id}");
            public static void HandlingSubscriptionChange(string content) => Log.LogDebug((int)EventIds.HandlingSubscriptionChange, $"Handling subscription change {content}");
        }
    }
}
