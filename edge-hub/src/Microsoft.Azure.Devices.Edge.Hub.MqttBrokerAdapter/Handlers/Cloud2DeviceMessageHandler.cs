// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client.Common;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    public class Cloud2DeviceMessageHandler : ICloud2DeviceMessageHandler, IMessageConsumer, IMessageProducer
    {
        const string SubscriptionChangePattern = @"^\$edgehub/(?<id1>[^/\+\#]+)(/(?<id2>[^/\+\#]+))?/subscriptions$";
        const string SubscriptionForDeviceboundPattern = @"^\$edgehub/(?<id1>[^/\+\#]+)/messages/c2d/post/\#$";

        const string C2DTopicDeviceTemplate = "$edgehub/{0}/messages/c2d/post/{1}";

        static readonly char[] identitySegmentSeparator = new[] { '/' };

        readonly IConnectionRegistry connectionRegistry;
        IMqttBridgeConnector connector;

        public Cloud2DeviceMessageHandler(IConnectionRegistry connectionRegistry) => this.connectionRegistry = connectionRegistry;

        public Task<bool> HandleAsync(MqttPublishInfo publishInfo)
        {
            var match = Regex.Match(publishInfo.Topic, SubscriptionChangePattern);
            if (match.Success)
            {
                return this.HandleSubscriptionChanged(match, publishInfo);
            }

            return Task.FromResult(false);
        }

        public void SetConnector(IMqttBridgeConnector connector)
        {
            this.connector = connector;
        }

        public async Task SendC2DMessageAsync(IMessage message, IIdentity identity)
        {
            bool result;
            try
            {
                var properties = new Dictionary<string, string>(message.Properties);

                foreach (KeyValuePair<string, string> systemProperty in message.SystemProperties)
                {
                    if (SystemProperties.OutgoingSystemPropertiesMap.TryGetValue(systemProperty.Key, out string onWirePropertyName))
                    {
                        properties[onWirePropertyName] = systemProperty.Value;
                    }
                }

                var propertyBag = UrlEncodedDictionarySerializer.Serialize(message.Properties.Concat(message.SystemProperties));

                result = await this.connector.SendAsync(
                                                GetCloudToDeviceTopic(identity, propertyBag),
                                                message.Body);
            }
            catch (Exception e)
            {
                Events.FailedToSendCloudToDeviceMessage(e);
                result = false;
            }

            if (result)
            {
                Events.CouldToDeviceMessage(identity.Id, message.Body.Length);

                // TODO: confirming back the message based on the fact that the MQTT broker ACK-ed it. It doesn't mean that the
                // C2D message has been delivered. Going forward it is a broker responsibility to deliver the message, however if
                // it crashes, the message will be lost
                await this.ConfirmMessage(message, identity);
            }
            else
            {
                Events.CouldToDeviceMessageFailed(identity.Id, message.Body.Length);
            }
        }

        async Task ConfirmMessage(IMessage message, IIdentity identity)
        {
            var proxy = default(IDeviceListener);
            try
            {
                proxy = (await this.connectionRegistry.GetUpstreamProxyAsync(identity)).Expect(() => new Exception($"No upstream proxy found for {identity.Id}"));
            }
            catch (Exception)
            {
                Events.MissingProxy(identity.Id);
                return;
            }

            var lockToken = "Unknown";
            try
            {
                lockToken = message.SystemProperties[SystemProperties.LockToken];
                await proxy.ProcessMessageFeedbackAsync(lockToken, FeedbackStatus.Complete);
            }
            catch (Exception ex)
            {
                Events.FailedToConfirm(ex, lockToken, identity.Id);
            }
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

            var subscribesC2D = false;

            foreach (var subscription in subscriptionList)
            {
                var subscriptionMatch = Regex.Match(subscription, SubscriptionForDeviceboundPattern);
                if (IsMatchWithIds(subscriptionMatch, id1, id2))
                {
                    subscribesC2D = true;
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

            await AddOrRemove(proxy, subscribesC2D, DeviceSubscription.C2D);

            return true;
        }

        static string GetCloudToDeviceTopic(IIdentity identity, string propertyBag)
        {
            var identityComponents = identity.Id.Split(identitySegmentSeparator, StringSplitOptions.RemoveEmptyEntries);

            switch (identityComponents.Length)
            {
                case 1:
                    return string.Format(C2DTopicDeviceTemplate, identityComponents[0], propertyBag);

                case 2:
                    Events.CannotSendC2DToModule(identity.Id);
                    throw new Exception($"Cannot send C2D message to {identity.Id}, because it is not a device but a module");

                default:
                    Events.BadIdentityFormat(identity.Id);
                    throw new Exception($"cannot decode identity {identity.Id}");
            }
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
            const int IdStart = MqttBridgeEventIds.Cloud2DeviceMessageHandler;
            static readonly ILogger Log = Logger.Factory.CreateLogger<Cloud2DeviceMessageHandler>();

            enum EventIds
            {
                BadPayloadFormat = IdStart,
                MissingProxy,
                FailedToSendCloudToDeviceMessage,
                CouldToDeviceMessage,
                CouldToDeviceMessageFailed,
                BadIdentityFormat,
                CannotSendC2DToModule,
                FailedToConfirm,
            }

            public static void MissingProxy(string id) => Log.LogError((int)EventIds.MissingProxy, $"Missing proxy for [{id}]");
            public static void BadPayloadFormat(Exception e) => Log.LogError((int)EventIds.BadPayloadFormat, e, "Bad payload format: cannot deserialize subscription update");
            public static void FailedToSendCloudToDeviceMessage(Exception e) => Log.LogError((int)EventIds.FailedToSendCloudToDeviceMessage, e, "Failed to send Cloud to Device message");
            public static void CouldToDeviceMessage(string id, int messageLen) => Log.LogDebug((int)EventIds.CouldToDeviceMessage, $"Cloud to Device message sent to client: [{id}], msg len: [{messageLen}]");
            public static void CouldToDeviceMessageFailed(string id, int messageLen) => Log.LogError((int)EventIds.CouldToDeviceMessageFailed, $"Failed to send Cloud to Device message to client: [{id}], msg len: [{messageLen}]");
            public static void BadIdentityFormat(string identity) => Log.LogError((int)EventIds.BadIdentityFormat, $"Bad identity format: {identity}");
            public static void CannotSendC2DToModule(string id) => Log.LogError((int)EventIds.CannotSendC2DToModule, $"Cannot send C2D message to module [{id}]");
            public static void FailedToConfirm(Exception ex, string lockToken, string id) => Log.LogError((int)EventIds.FailedToConfirm, ex, $"Cannot confirm back delivered C2D message to [{id}] with token [{lockToken}]");
        }
    }
}
