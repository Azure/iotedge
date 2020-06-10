// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter.Handlers
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    public class DirectMethodHandler : IDirectMethodHandler, ISubscriber, IMessageConsumer
    {
        const string MethodPostModule = "$edgehub/+/+/methods/post/#";
        const string MethodPostDevice = "$edgehub/+/methods/post/#";

        const string SubscriptionChangePattern = @"^\$edgehub/(?<id1>[^/\+\#]+)(/(?<id2>[^/\+\#]+))?/subscriptions$";

        const string MethodSubscriptionForPostPattern = @"^\$edgehub/(?<id1>[^/\+\#]+)(/(?<id2>[^/\+\#]+))?/methods/post/\#$";

        static readonly string[] subscriptions = new[] { MethodPostModule, MethodPostDevice };

        readonly IConnectionRegistry connectionRegistry;

        public IReadOnlyCollection<string> Subscriptions => subscriptions;

        public DirectMethodHandler(IConnectionRegistry connectionRegistry) => this.connectionRegistry = connectionRegistry;

        public Task<bool> HandleAsync(MqttPublishInfo publishInfo)
        {
            var match = Regex.Match(publishInfo.Topic, SubscriptionChangePattern);
            if (match.Success)
            {
                return this.HandleSubscriptionChanged(match, publishInfo);
            }

            return Task.FromResult(false);
        }

        public Task<DirectMethodResponse> CallDirectMethodAsync(DirectMethodRequest request, IIdentity identity)
        {
            throw new NotImplementedException();
        }

        async Task<bool> HandleSubscriptionChanged(Match match, MqttPublishInfo publishInfo)
        {
            var id1 = match.Groups["id1"];
            var id2 = match.Groups["id2"];

            var identity = GetIdentityFromIdParts(id1, id2);

            var subscriptionList = default(List<string>);
            try
            {
                var payloadAsString = Encoding.UTF8.GetString(publishInfo.Payload);
                subscriptionList = JsonConvert.DeserializeObject<List<string>>(payloadAsString);
            }
            catch (Exception e)
            {
                Events.BadPayloadFormat(e);
                return false;
            }

            var subscribesMethodPost = false;

            foreach (var subscription in subscriptionList)
            {
                var subscriptionMatch = Regex.Match(subscription, MethodSubscriptionForPostPattern);
                if (IsMatchWithIds(subscriptionMatch, id1, id2))
                {
                    subscribesMethodPost = true;
                    break;
                }
            }

            var proxy = default(IDeviceListener);
            try
            {
                proxy = (await this.connectionRegistry.GetUpstreamProxyAsync(identity)).Expect(() => new Exception($"No upstream proxy found for {identity.Id}"));
            }
            catch (Exception)
            {
                Events.MissingProxy(identity.Id);
                return false;
            }

            await AddOrRemove(proxy, subscribesMethodPost, DeviceSubscription.Methods);

            return true;
        }

        static Task AddOrRemove(IDeviceListener proxy, bool add, DeviceSubscription subscription)
        {
            if (add)
            {
                return proxy.AddSubscription(subscription);
            }
            else
            {
                return proxy.RemoveSubscription(subscription);
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

        static IIdentity GetIdentityFromIdParts(Group id1, Group id2)
        {
            if (id2.Success)
            {
                // FIXME the iothub name should come from somewhere
                return new ModuleIdentity("vikauthtest.azure-devices.net", id1.Value, id2.Value);
            }
            else
            {
                // FIXME the iothub name should come from somewhere
                return new DeviceIdentity("vikauthtest.azure-devices.net", id1.Value);
            }
        }

        static class Events
        {
            const int IdStart = MqttBridgeEventIds.DirectMethodHandler;
            static readonly ILogger Log = Logger.Factory.CreateLogger<DirectMethodHandler>();

            enum EventIds
            {
                MissingProxy,
                BadPayloadFormat
            }

            public static void MissingProxy(string id) => Log.LogError((int)EventIds.MissingProxy, $"Missing proxy for [{id}]");
            public static void BadPayloadFormat(Exception e) => Log.LogError((int)EventIds.BadPayloadFormat, e, "Bad payload format: cannot deserialize subscription update");
        }
    }
}
