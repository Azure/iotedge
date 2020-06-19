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

    public class SubscriptionChangeHandler : ISubscriptionChangeHandler
    {
        const string SubscriptionChangePattern = @"^\$edgehub/(?<id1>[^/\+\#]+)(/(?<id2>[^/\+\#]+))?/subscriptions$";

        readonly IConnectionRegistry connectionRegistry;
        readonly IComponentDiscovery components;
        readonly IIdentityProvider identityProvider;

        public SubscriptionChangeHandler(IConnectionRegistry connectionRegistry, IComponentDiscovery components, IIdentityProvider identityProvider)
        {
            this.connectionRegistry = connectionRegistry;
            this.components = components;
            this.identityProvider = identityProvider;
        }

        public async Task<bool> HandleSubscriptionChangeAsync(MqttPublishInfo publishInfo)
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

            var identity = HandlerUtils.GetIdentityFromMatch(id1, id2, this.identityProvider);

            var proxy = default(IDeviceListener);
            var proxyMaybe = await this.connectionRegistry.GetUpstreamProxyAsync(identity);

            if (!proxyMaybe.HasValue)
            {
                return true;
            }
            else
            {
                proxy = proxyMaybe.Expect(() => new Exception($"No upstream proxy found for {identity.Id}"));
            }

            foreach (var watcher in this.components.SubscriptionWatchers)
            {
                foreach (var subscriptionPattern in watcher.WatchedSubscriptions)
                {
                    var subscribes = false;

                    foreach (var subscription in subscriptionList)
                    {
                        var subscriptionMatch = Regex.Match(subscription, subscriptionPattern.Pattern);
                        if (subscriptionMatch.IsMatchWithIds(id1, id2))
                        {
                            subscribes = true;
                            break;
                        }
                    }

                    try
                    {
                        await proxy.AddOrRemoveSubscription(subscribes, subscriptionPattern.Subscrition);
                    }
                    catch (Exception e)
                    {
                        Events.FailedToChangeSubscriptionState(e, subscriptionPattern.Subscrition.ToString(), identity.Id);
                    }
                }
            }

            return true;
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
            public static void FailedToChangeSubscriptionState(Exception e, string subscription, string id) => Log.LogError((int)EventIds.FailedToChangeSubscriptionState, e, $"Failed to change subscrition status [{subscription}] for [{id}]");
            public static void HandlingSubscriptionChange(string content) => Log.LogDebug((int)EventIds.HandlingSubscriptionChange, $"Handling subscription change [{content}]");
        }
    }
}
