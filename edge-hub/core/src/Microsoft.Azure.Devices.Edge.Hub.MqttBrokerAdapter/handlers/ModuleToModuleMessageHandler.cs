// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class ModuleToModuleMessageHandler : MessageConfirmingHandler, IModuleToModuleMessageHandler, IMessageProducer, IMessageConsumer
    {
        const string MessageDelivered = "$edgehub/delivered";
        const string MessageDeliveredSubscription = MessageDelivered + "/#";
        const string ModuleToModleSubscriptionPattern = @"^\$edgehub/(?<id1>[^/\+\#]+)/(?<id2>[^/\+\#]+)/inputs/\#$";
        const string FeedbackMessagePattern = @"^\""\$edgehub/(?<id1>[^/\+\#]+)/(?<id2>[^/\+\#]+)/inputs/";
        const string ModuleToModleTopicTemplate = @"$edgehub/{0}/{1}/inputs/{2}/{3}";

        static readonly SubscriptionPattern[] subscriptionPatterns = new SubscriptionPattern[] { new SubscriptionPattern(ModuleToModleSubscriptionPattern, DeviceSubscription.ModuleMessages) };

        IMqttBrokerConnector connector;
        IIdentityProvider identityProvider;
        ConcurrentDictionary<IIdentity, string> pendingMessages = new ConcurrentDictionary<IIdentity, string>();

        public ModuleToModuleMessageHandler(IConnectionRegistry connectionRegistry, IIdentityProvider identityProvider)
            : base(connectionRegistry)
        {
            this.identityProvider = identityProvider;
        }

        public void SetConnector(IMqttBrokerConnector connector) => this.connector = connector;
        public IReadOnlyCollection<SubscriptionPattern> WatchedSubscriptions => subscriptionPatterns;

        public IReadOnlyCollection<string> Subscriptions => new[] { MessageDeliveredSubscription };

        public async Task<bool> HandleAsync(MqttPublishInfo publishInfo)
        {
            if (publishInfo.Topic.Equals(MessageDelivered))
            {
                try
                {
                    var originalTopic = Encoding.UTF8.GetString(publishInfo.Payload);
                    var match = Regex.Match(originalTopic, FeedbackMessagePattern);
                    if (match.Success)
                    {
                        var id1 = match.Groups["id1"];
                        var id2 = match.Groups["id2"];

                        var identity = this.identityProvider.Create(id1.Value, id2.Value);

                        if (this.pendingMessages.TryRemove(identity, out var lockToken))
                        {
                            await this.ConfirmMessageAsync(lockToken, identity);
                        }
                        else
                        {
                            Events.CannotFindMessageToConfirm(identity.Id);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Events.CannotDecodeConfirmation(ex);
                }

                return true;
            }
            else
            {
                return false;
            }
        }

        public async Task SendModuleToModuleMessageAsync(IMessage message, string input, IIdentity identity)
        {
            if (!message.SystemProperties.TryGetValue(SystemProperties.LockToken, out var currentLockToken))
            {
                Events.NoLockToken(identity.Id);
                throw new Exception("Cannot send M2M message without lock token");
            }

            bool result;
            var topic = string.Empty;
            try
            {
                var propertyBag = GetPropertyBag(message);
                topic = GetMessageToMessageTopic(identity, input, propertyBag);

                result = await this.connector.SendAsync(topic, message.Body);
            }
            catch (Exception e)
            {
                Events.FailedToSendModuleToModuleMessage(e);
                result = false;
            }

            if (result)
            {
                Events.ModuleToModuleMessage(identity.Id, message.Body.Length);

                var overwrittenLockToken = default(string);
                this.pendingMessages.AddOrUpdate(
                        identity,
                        currentLockToken,
                        (i, t) =>
                        {
                            overwrittenLockToken = t;
                            return currentLockToken;
                        });

                if (overwrittenLockToken != null)
                {
                    Events.OverwritingPendingMessage(identity.Id, overwrittenLockToken);
                }
            }
            else
            {
                Events.ModuleToModuleMessageFailed(identity.Id, message.Body.Length);
            }
        }

        static string GetMessageToMessageTopic(IIdentity identity, string input, string propertyBag)
        {
            switch (identity)
            {
                case IDeviceIdentity deviceIdentity:
                    Events.CannotSendM2MToDevice(identity.Id);
                    throw new Exception($"Cannot send Module To Module message to {identity.Id}, because it is not a module but a device");

                case IModuleIdentity moduleIdentity:
                    return string.Format(ModuleToModleTopicTemplate, moduleIdentity.DeviceId, moduleIdentity.ModuleId, input, propertyBag);

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
                CannotDecodeConfirmation,
                OverwritingPendingMessage,
                CannotFindMessageToConfirm,
                NoLockToken,
            }

            public static void BadPayloadFormat(Exception e) => Log.LogError((int)EventIds.BadPayloadFormat, e, "Bad payload format: cannot deserialize subscription update");
            public static void FailedToSendModuleToModuleMessage(Exception e) => Log.LogError((int)EventIds.FailedToSendModuleToModuleMessage, e, "Failed to send Module to Module message");
            public static void ModuleToModuleMessage(string id, int messageLen) => Log.LogDebug((int)EventIds.ModuleToModuleMessage, $"Module to Module message sent to client: {id}, msg len: {messageLen}");
            public static void ModuleToModuleMessageFailed(string id, int messageLen) => Log.LogError((int)EventIds.ModuleToModuleMessageFailed, $"Failed to send Module to Module message to client: {id}, msg len: {messageLen}");
            public static void BadIdentityFormat(string identity) => Log.LogError((int)EventIds.BadIdentityFormat, $"Bad identity format: {identity}");
            public static void CannotSendM2MToDevice(string id) => Log.LogError((int)EventIds.CannotSendM2MToDevice, $"Cannot send Module to Module message to device {id}");
            public static void CannotDecodeConfirmation(Exception e) => Log.LogError((int)EventIds.CannotDecodeConfirmation, e, $"Cannot decode Module to Module message confirmation");
            public static void OverwritingPendingMessage(string identity, string messageId) => Log.LogWarning((int)EventIds.OverwritingPendingMessage, $"New M2M message is being sent for {identity} while the previous has not been acknowledged with msg id {messageId}");
            public static void CannotFindMessageToConfirm(string identity) => Log.LogWarning((int)EventIds.CannotFindMessageToConfirm, $"M2M confirmation has received for {identity} but no message can be found");
            public static void NoLockToken(string identity) => Log.LogError((int)EventIds.NoLockToken, $"Cannot send M2M message for {identity} because it does not have lock token in its system properties");
        }
    }
}
