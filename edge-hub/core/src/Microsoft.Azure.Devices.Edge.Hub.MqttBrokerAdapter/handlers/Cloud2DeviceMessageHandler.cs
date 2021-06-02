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

    public class Cloud2DeviceMessageHandler : MessageConfirmingHandler, ICloud2DeviceMessageHandler, IMessageProducer
    {
        const string SubscriptionForDeviceboundPattern = @"^((?<dialect>(\$edgehub)|(\$iothub)))/(?<id1>[^/\+\#]+)/messages/c2d/post/\#$";
        const string C2DTopicDeviceTemplate = "{0}/{1}/messages/c2d/post/{2}";

        static readonly SubscriptionPattern[] subscriptionPatterns = new SubscriptionPattern[] { new SubscriptionPattern(SubscriptionForDeviceboundPattern, DeviceSubscription.C2D) };

        IMqttBrokerConnector connector;

        public Cloud2DeviceMessageHandler(IConnectionRegistry connectionRegistry)
            : base(connectionRegistry)
        {
        }

        public IReadOnlyCollection<SubscriptionPattern> WatchedSubscriptions => subscriptionPatterns;
        public void SetConnector(IMqttBrokerConnector connector) => this.connector = connector;

        public async Task SendC2DMessageAsync(IMessage message, IIdentity identity, bool isDirectClient)
        {
            if (!message.SystemProperties.TryGetValue(SystemProperties.LockToken, out var lockToken))
            {
                Events.NoLockToken(identity.Id);
                throw new Exception("Cannot send C2D message without lock token");
            }

            bool result;
            try
            {
                var topicPrefix = isDirectClient ? MqttBrokerAdapterConstants.DirectTopicPrefix : MqttBrokerAdapterConstants.IndirectTopicPrefix;
                var propertyBag = GetPropertyBag(message);
                result = await this.connector.SendAsync(
                                                GetCloudToDeviceTopic(identity, propertyBag, topicPrefix),
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
                await this.ConfirmMessageAsync(lockToken, identity);
            }
            else
            {
                Events.CouldToDeviceMessageFailed(identity.Id, message.Body.Length);
            }
        }

        static string GetCloudToDeviceTopic(IIdentity identity, string propertyBag, string topicPrefix)
        {
            switch (identity)
            {
                case IDeviceIdentity deviceIdentity:
                    return string.Format(C2DTopicDeviceTemplate, topicPrefix, deviceIdentity.DeviceId, propertyBag);

                case IModuleIdentity _:
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
                NoLockToken
            }

            public static void BadPayloadFormat(Exception e) => Log.LogError((int)EventIds.BadPayloadFormat, e, "Bad payload format: cannot deserialize subscription update");
            public static void FailedToSendCloudToDeviceMessage(Exception e) => Log.LogError((int)EventIds.FailedToSendCloudToDeviceMessage, e, "Failed to send Cloud to Device message");
            public static void CouldToDeviceMessage(string id, int messageLen) => Log.LogDebug((int)EventIds.CouldToDeviceMessage, $"Cloud to Device message sent to client: {id}, msg len: {messageLen}");
            public static void CouldToDeviceMessageFailed(string id, int messageLen) => Log.LogError((int)EventIds.CouldToDeviceMessageFailed, $"Failed to send Cloud to Device message to client: {id}, msg len: {messageLen}");
            public static void BadIdentityFormat(string identity) => Log.LogError((int)EventIds.BadIdentityFormat, $"Bad identity format: {identity}");
            public static void CannotSendC2DToModule(string id) => Log.LogError((int)EventIds.CannotSendC2DToModule, $"Cannot send C2D message to module {id}");
            public static void NoLockToken(string identity) => Log.LogError((int)EventIds.NoLockToken, $"Cannot send C2D message for {identity} because it does not have lock token in its system properties");
        }
    }
}
