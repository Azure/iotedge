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

    public class ModuleToModuleMessageHandler : MessageConfirmingHandler, IModuleToModuleMessageHandler, IMessageProducer
    {
        const string ModuleToModleSubscriptionPattern = @"^((?<dialect>(\$edgehub)|(\$iothub)))/(?<id1>[^/\+\#]+)/(?<id2>[^/\+\#]+)/inputs/\#$";
        const string ModuleToModleTopicTemplate = @"{0}/{1}/{2}/inputs/{3}/{4}";

        static readonly SubscriptionPattern[] subscriptionPatterns = new SubscriptionPattern[] { new SubscriptionPattern(ModuleToModleSubscriptionPattern, DeviceSubscription.ModuleMessages) };

        IMqttBrokerConnector connector;

        public ModuleToModuleMessageHandler(IConnectionRegistry connectionRegistry)
            : base(connectionRegistry)
        {
        }

        public void SetConnector(IMqttBrokerConnector connector) => this.connector = connector;
        public IReadOnlyCollection<SubscriptionPattern> WatchedSubscriptions => subscriptionPatterns;

        public async Task SendModuleToModuleMessageAsync(IMessage message, string input, IIdentity identity, bool isDirectClient)
        {
            bool result;
            try
            {
                var topicPrefix = isDirectClient ? MqttBrokerAdapterConstants.DirectTopicPrefix : MqttBrokerAdapterConstants.IndirectTopicPrefix;
                var propertyBag = GetPropertyBag(message);
                result = await this.connector.SendAsync(
                                                GetMessageToMessageTopic(identity, input, propertyBag, topicPrefix),
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

        static string GetMessageToMessageTopic(IIdentity identity, string input, string propertyBag, string topicPrefix)
        {
            switch (identity)
            {
                case IDeviceIdentity deviceIdentity:
                    Events.CannotSendM2MToDevice(identity.Id);
                    throw new Exception($"Cannot send Module To Module message to {identity.Id}, because it is not a module but a device");

                case IModuleIdentity moduleIdentity:
                    return string.Format(ModuleToModleTopicTemplate, topicPrefix, moduleIdentity.DeviceId, moduleIdentity.ModuleId, input, propertyBag);

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
            public static void ModuleToModuleMessage(string id, int messageLen) => Log.LogDebug((int)EventIds.ModuleToModuleMessage, $"Module to Module message sent to client: {id}, msg len: {messageLen}");
            public static void ModuleToModuleMessageFailed(string id, int messageLen) => Log.LogError((int)EventIds.ModuleToModuleMessageFailed, $"Failed to send Module to Module message to client: {id}, msg len: {messageLen}");
            public static void BadIdentityFormat(string identity) => Log.LogError((int)EventIds.BadIdentityFormat, $"Bad identity format: {identity}");
            public static void CannotSendM2MToDevice(string id) => Log.LogError((int)EventIds.CannotSendM2MToDevice, $"Cannot send Module to Module message to device {id}");
        }
    }
}
