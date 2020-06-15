// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class Cloud2DeviceMessageHandler : MessageConfirmingHandler, ICloud2DeviceMessageHandler, IMessageProducer, ISubscriptionWatcher
    {
        const string SubscriptionForDeviceboundPattern = @"^\$edgehub/(?<id1>[^/\+\#]+)/messages/c2d/post/\#$";
        const string C2DTopicDeviceTemplate = "$edgehub/{0}/messages/c2d/post/{1}";

        static readonly SubscriptionPattern[] subscriptionPatterns = new SubscriptionPattern[] { new SubscriptionPattern(SubscriptionForDeviceboundPattern, DeviceSubscription.C2D) };

        IMqttBridgeConnector connector;

        public Cloud2DeviceMessageHandler(IConnectionRegistry connectionRegistry) : base(connectionRegistry) { }
        public IReadOnlyCollection<SubscriptionPattern> WatchedSubscriptions => subscriptionPatterns;
        public void SetConnector(IMqttBridgeConnector connector) => this.connector = connector;

        public async Task SendC2DMessageAsync(IMessage message, IIdentity identity)
        {
            bool result;
            try
            {
                var propertyBag = HandlerUtils.GetPropertyBag(message);                
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
                await this.ConfirmMessageAsync(message, identity);
            }
            else
            {
                Events.CouldToDeviceMessageFailed(identity.Id, message.Body.Length);
            }
        }

        static string GetCloudToDeviceTopic(IIdentity identity, string propertyBag)
        {
            var identityComponents = identity.Id.Split(HandlerUtils.IdentitySegmentSeparator, StringSplitOptions.RemoveEmptyEntries);

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

        static class Events
        {
            const int IdStart = MqttBridgeEventIds.Cloud2DeviceMessageHandler;
            static readonly ILogger Log = Logger.Factory.CreateLogger<Cloud2DeviceMessageHandler>();

            enum EventIds
            {
                BadPayloadFormat = IdStart,
                FailedToSendCloudToDeviceMessage,
                CouldToDeviceMessage,
                CouldToDeviceMessageFailed,
                BadIdentityFormat,
                CannotSendC2DToModule,
            }
            
            public static void BadPayloadFormat(Exception e) => Log.LogError((int)EventIds.BadPayloadFormat, e, "Bad payload format: cannot deserialize subscription update");
            public static void FailedToSendCloudToDeviceMessage(Exception e) => Log.LogError((int)EventIds.FailedToSendCloudToDeviceMessage, e, "Failed to send Cloud to Device message");
            public static void CouldToDeviceMessage(string id, int messageLen) => Log.LogDebug((int)EventIds.CouldToDeviceMessage, $"Cloud to Device message sent to client: [{id}], msg len: [{messageLen}]");
            public static void CouldToDeviceMessageFailed(string id, int messageLen) => Log.LogError((int)EventIds.CouldToDeviceMessageFailed, $"Failed to send Cloud to Device message to client: [{id}], msg len: [{messageLen}]");
            public static void BadIdentityFormat(string identity) => Log.LogError((int)EventIds.BadIdentityFormat, $"Bad identity format: {identity}");
            public static void CannotSendC2DToModule(string id) => Log.LogError((int)EventIds.CannotSendC2DToModule, $"Cannot send C2D message to module [{id}]");
        }
    }
}
