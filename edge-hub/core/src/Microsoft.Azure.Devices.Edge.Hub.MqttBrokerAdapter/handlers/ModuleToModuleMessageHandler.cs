// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class ModuleToModuleMessageHandler : MessageConfirmingHandler, IModuleToModuleMessageHandler, IMessageProducer, IMessageConsumer, IDisposable
    {
        const string MessageDelivered = "$edgehub/delivered";
        const string MessageDeliveredSubscription = MessageDelivered + "/#";
        const string ModuleToModleSubscriptionPattern = @"^((?<dialect>(\$edgehub)|(\$iothub)))/(?<id1>[^/\+\#]+)/(?<id2>[^/\+\#]+)/\+/inputs/\#$";
        const string FeedbackMessagePattern = @"^\""\$edgehub/(?<id1>[^/\+\#]+)/(?<id2>[^/\+\#]+)/(?<token>[^/\+\#]+)/inputs/";
        const string ModuleToModleTopicTemplate = @"{0}/{1}/{2}/{3}/inputs/{4}/{5}";

        static readonly SubscriptionPattern[] subscriptionPatterns = new SubscriptionPattern[] { new SubscriptionPattern(ModuleToModleSubscriptionPattern, DeviceSubscription.ModuleMessages) };

        readonly Timer timer;
        readonly TimeSpan tokenCleanupPeriod;

        IMqttBrokerConnector connector;
        IIdentityProvider identityProvider;
        ConcurrentDictionary<string, DateTime> pendingMessages = new ConcurrentDictionary<string, DateTime>();

        public ModuleToModuleMessageHandler(IConnectionRegistry connectionRegistry, IIdentityProvider identityProvider, ModuleToModuleResponseTimeout responseTimeout)
            : base(connectionRegistry)
        {
            this.identityProvider = Preconditions.CheckNotNull(identityProvider);
            this.tokenCleanupPeriod = responseTimeout;
            this.timer = new Timer(this.CleanTokens, null, responseTimeout, responseTimeout);
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
                        var lockToken = match.Groups["token"].Value;

                        var identity = this.identityProvider.Create(id1.Value, id2.Value);

                        if (this.pendingMessages.TryRemove(lockToken, out var _))
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

        public async Task SendModuleToModuleMessageAsync(IMessage message, string input, IIdentity identity, bool isDirectClient)
        {
            if (!message.SystemProperties.TryGetValue(SystemProperties.LockToken, out var currentLockToken))
            {
                Events.NoLockToken(identity.Id);
                throw new ArgumentException("Cannot send M2M message without lock token");
            }

            bool result;
            try
            {
                var currentTime = DateTime.UtcNow;
                var overwrittenLockTokenDate = Option.None<DateTime>();
                this.pendingMessages.AddOrUpdate(
                        currentLockToken,
                        currentTime,
                        (i, t) =>
                        {
                            overwrittenLockTokenDate = Option.Some(t);
                            return currentTime;
                        });

                overwrittenLockTokenDate.ForEach(t => Events.OverwritingPendingMessage(identity.Id, currentLockToken, t));

                var topicPrefix = isDirectClient ? MqttBrokerAdapterConstants.DirectTopicPrefix : MqttBrokerAdapterConstants.IndirectTopicPrefix;
                var propertyBag = GetPropertyBag(message);
                result = await this.connector.SendAsync(
                                                GetMessageToMessageTopic(identity, input, propertyBag, topicPrefix, currentLockToken),
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
            }
            else
            {
                this.pendingMessages.TryRemove(currentLockToken, out var _);
                Events.ModuleToModuleMessageFailed(identity.Id, message.Body.Length);
            }
        }

        public void Dispose()
        {
            this.timer.Dispose();
        }

        void CleanTokens(object _)
        {
            var now = DateTime.UtcNow;
            var keys = this.pendingMessages.Keys.ToArray();

            foreach (var key in keys)
            {
                if (this.pendingMessages.TryGetValue(key, out DateTime issued))
                {
                    if (now - issued > this.tokenCleanupPeriod)
                    {
                        Events.RemovingExpiredToken(key);
                        this.pendingMessages.TryRemove(key, out var _);
                    }
                }
            }
        }

        static string GetMessageToMessageTopic(IIdentity identity, string input, string propertyBag, string topicPrefix, string lockToken)
        {
            switch (identity)
            {
                case IDeviceIdentity deviceIdentity:
                    Events.CannotSendM2MToDevice(identity.Id);
                    throw new ArgumentException($"Cannot send Module To Module message to {identity.Id}, because it is not a module but a device");

                case IModuleIdentity moduleIdentity:
                    return string.Format(ModuleToModleTopicTemplate, topicPrefix, moduleIdentity.DeviceId, moduleIdentity.ModuleId, lockToken, input, propertyBag);

                default:
                    Events.BadIdentityFormat(identity.Id);
                    throw new ArgumentException($"cannot decode identity {identity.Id}");
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
                RemovingExpiredToken
            }

            public static void BadPayloadFormat(Exception e) => Log.LogError((int)EventIds.BadPayloadFormat, e, "Bad payload format: cannot deserialize subscription update");
            public static void FailedToSendModuleToModuleMessage(Exception e) => Log.LogError((int)EventIds.FailedToSendModuleToModuleMessage, e, "Failed to send Module to Module message");
            public static void ModuleToModuleMessage(string id, int messageLen) => Log.LogDebug((int)EventIds.ModuleToModuleMessage, $"Module to Module message sent to client: {id}, msg len: {messageLen}");
            public static void ModuleToModuleMessageFailed(string id, int messageLen) => Log.LogError((int)EventIds.ModuleToModuleMessageFailed, $"Failed to send Module to Module message to client: {id}, msg len: {messageLen}");
            public static void BadIdentityFormat(string identity) => Log.LogError((int)EventIds.BadIdentityFormat, $"Bad identity format: {identity}");
            public static void CannotSendM2MToDevice(string id) => Log.LogError((int)EventIds.CannotSendM2MToDevice, $"Cannot send Module to Module message to device {id}");
            public static void CannotDecodeConfirmation(Exception e) => Log.LogError((int)EventIds.CannotDecodeConfirmation, e, $"Cannot decode Module to Module message confirmation");
            public static void OverwritingPendingMessage(string identity, string messageId, DateTime time) => Log.LogWarning((int)EventIds.OverwritingPendingMessage, $"New M2M message is being sent for {identity} with msg id {messageId}, but it has been sent already with the same id at {time}");
            public static void CannotFindMessageToConfirm(string identity) => Log.LogWarning((int)EventIds.CannotFindMessageToConfirm, $"M2M confirmation has received for {identity} but no message can be found");
            public static void NoLockToken(string identity) => Log.LogError((int)EventIds.NoLockToken, $"Cannot send M2M message for {identity} because it does not have lock token in its system properties");
            public static void RemovingExpiredToken(string token) => Log.LogWarning((int)EventIds.RemovingExpiredToken, $"M2M confirmation has not been received for token {token}, removing");
        }
    }
}
