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

    public class ModuleToModuleMessageHandler : MessageConfirmingHandler, IModuleToModuleMessageHandler, IMessageProducer, ISubscriptionWatcher
    {
        // FIXME change when topic translation is fixed
        const string ModuleToModleSubscriptionPattern = @"^devices/(?<id1>[^/\+\#]+)/modules/(?<id2>[^/\+\#]+)/\#$";
        const string ModuleToModleTopicTemplate = @"devices/{0}/modules/{1}/inputs/{2}/{2}";

        static readonly SubscriptionPattern[] subscriptionPatterns = new SubscriptionPattern[] { new SubscriptionPattern(ModuleToModleSubscriptionPattern, DeviceSubscription.ModuleMessages) };

        IMqttBridgeConnector connector;

        public ModuleToModuleMessageHandler(IConnectionRegistry connectionRegistry) : base(connectionRegistry) { }
        public void SetConnector(IMqttBridgeConnector connector) => this.connector = connector;
        public IReadOnlyCollection<SubscriptionPattern> WatchedSubscriptions => subscriptionPatterns;

        public async Task SendModuleToModuleMessageAsync(IMessage message, string input, IIdentity identity)
        {
            bool result;
            try
            {
                var propertyBag = HandlerUtils.GetPropertyBag(message);
                result = await this.connector.SendAsync(
                                                GetMessageToMessageTopic(identity, input, propertyBag),
                                                message.Body);
            }
            catch (Exception e)
            {
                Events.FailedToSendModuleToModuleMessage(e);
                result = false;
            }

            if (result)
            {
                Events.ModuleToModuleMessage(identity.Id, message.Body.Length);

                // TODO: confirming back the message based on the fact that the MQTT broker ACK-ed it. It doesn't mean that the
                // M2M message has been delivered. Going forward it is a broker responsibility to deliver the message, however if
                // it crashes, the message will be lost
                await this.ConfirmMessageAsync(message, identity);
            }
            else
            {
                Events.ModuleToModuleMessageFailed(identity.Id, message.Body.Length);
            }
        }

        static string GetMessageToMessageTopic(IIdentity identity, string input, string propertyBag)
        {
            var identityComponents = identity.Id.Split(HandlerUtils.IdentitySegmentSeparator, StringSplitOptions.RemoveEmptyEntries);

            switch (identityComponents.Length)
            {
                case 1:
                    Events.CannotSendM2MToDevice(identity.Id);
                    throw new Exception($"Cannot send C2D message to {identity.Id}, because it is not a device but a module");

                case 2:
                    return string.Format(ModuleToModleTopicTemplate, identityComponents[0], identityComponents[1], input, propertyBag);

                default:
                    Events.BadIdentityFormat(identity.Id);
                    throw new Exception($"cannot decode identity {identity.Id}");
            }
        }

        static class Events
        {
            const int IdStart = MqttBridgeEventIds.ModuleToModuleMessageHandler;
            static readonly ILogger Log = Logger.Factory.CreateLogger<ModuleToModuleMessageHandler>();

            enum EventIds
            {
                BadPayloadFormat = IdStart,
                FailedToSendModuleToModuleMessage,
                ModuleToModuleMessage,
                ModuleToModuleMessageFailed,
                BadIdentityFormat,
                CannotSendM2MToDevice,
            }
            
            public static void BadPayloadFormat(Exception e) => Log.LogError((int)EventIds.BadPayloadFormat, e, "Bad payload format: cannot deserialize subscription update");
            public static void FailedToSendModuleToModuleMessage(Exception e) => Log.LogError((int)EventIds.FailedToSendModuleToModuleMessage, e, "Failed to send Module to Module message");
            public static void ModuleToModuleMessage(string id, int messageLen) => Log.LogDebug((int)EventIds.ModuleToModuleMessage, $"Module to Module message sent to client: [{id}], msg len: [{messageLen}]");
            public static void ModuleToModuleMessageFailed(string id, int messageLen) => Log.LogError((int)EventIds.ModuleToModuleMessageFailed, $"Failed to send Module to Module message to client: [{id}], msg len: [{messageLen}]");
            public static void BadIdentityFormat(string identity) => Log.LogError((int)EventIds.BadIdentityFormat, $"Bad identity format: {identity}");
            public static void CannotSendM2MToDevice(string id) => Log.LogError((int)EventIds.CannotSendM2MToDevice, $"Cannot send Module to Module message to device [{id}]");
        }
    }
}
