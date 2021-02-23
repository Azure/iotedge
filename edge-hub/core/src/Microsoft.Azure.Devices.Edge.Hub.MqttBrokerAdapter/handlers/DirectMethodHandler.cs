// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class DirectMethodHandler : IDirectMethodHandler, IMessageConsumer, IMessageProducer
    {
        const string MethodPostDirectModule = "$edgehub/+/+/methods/res/#";
        const string MethodPostDirectDevice = "$edgehub/+/methods/res/#";
        const string MethodPostIndirectModule = "$iothub/+/+/methods/res/#";
        const string MethodPostIndirectDevice = "$iothub/+/methods/res/#";

        const string MethodSubscriptionForPostPattern = @"^((?<dialect>(\$edgehub)|(\$iothub)))/(?<id1>[^/\+\#]+)(/(?<id2>[^/\+\#]+))?/methods/post/\#$";
        const string MethodResponsePattern = @"^((?<dialect>(\$edgehub)|(\$iothub)))/(?<id1>[^/\+\#]+)(/(?<id2>[^/\+\#]+))?/methods/res/(?<res>\d+)/\?\$rid=(?<rid>.+)";

        const string MethodCallToDeviceTopicTemplate = "{0}/{1}/methods/post/{2}/?$rid={3}";
        const string MethodCallToModuleTopicTemplate = "{0}/{1}/{2}/methods/post/{3}/?$rid={4}";

        static readonly string[] subscriptions = new[] { MethodPostDirectModule, MethodPostDirectDevice, MethodPostIndirectModule, MethodPostIndirectDevice };

        static readonly SubscriptionPattern[] subscriptionPatterns = new SubscriptionPattern[] { new SubscriptionPattern(MethodSubscriptionForPostPattern, DeviceSubscription.Methods) };

        readonly IConnectionRegistry connectionRegistry;
        readonly IIdentityProvider identityProvider;

        IMqttBrokerConnector connector;

        public IReadOnlyCollection<string> Subscriptions => subscriptions;

        public DirectMethodHandler(IConnectionRegistry connectionRegistry, IIdentityProvider identityProvider)
        {
            this.connectionRegistry = Preconditions.CheckNotNull(connectionRegistry);
            this.identityProvider = Preconditions.CheckNotNull(identityProvider);
        }

        public IReadOnlyCollection<SubscriptionPattern> WatchedSubscriptions => subscriptionPatterns;

        public Task<bool> HandleAsync(MqttPublishInfo publishInfo)
        {
            var match = Regex.Match(publishInfo.Topic, MethodResponsePattern);
            if (match.Success)
            {
                return this.HandleMethodResponse(match, publishInfo);
            }

            return Task.FromResult(false);
        }

        public void SetConnector(IMqttBrokerConnector connector) => this.connector = connector;

        public async Task<DirectMethodResponse> CallDirectMethodAsync(DirectMethodRequest request, IIdentity identity, bool isDirectClient)
        {
            try
            {
                var topicPrefix = isDirectClient ? MqttBrokerAdapterConstants.DirectTopicPrefix : MqttBrokerAdapterConstants.IndirectTopicPrefix;
                var result = await this.connector.SendAsync(
                                            GetMethodCallTopic(identity, request.Name, request.CorrelationId, topicPrefix),
                                            request.Data);
                if (!result)
                {
                    throw new Exception($"MQTT transport failed to forward message for Direct Method call with rid [{request.CorrelationId}]");
                }

                return null;
            }
            catch (Exception e)
            {
                Events.FailedToSendDirectMethodMessage(e);
                return null;
            }
        }

        async Task<bool> HandleMethodResponse(Match match, MqttPublishInfo publishInfo)
        {
            var id1 = match.Groups["id1"];
            var id2 = match.Groups["id2"];
            var rid = match.Groups["rid"];
            var res = match.Groups["res"];

            var isDirect = string.Equals(match.Groups["dialect"].Value, MqttBrokerAdapterConstants.DirectTopicPrefix);

            var identity = id2.Success
                                ? this.identityProvider.Create(id1.Value, id2.Value)
                                : this.identityProvider.Create(id1.Value);

            var maybeListener = await this.connectionRegistry.GetOrCreateDeviceListenerAsync(identity, isDirect);
            var listener = default(IDeviceListener);

            try
            {
                listener = maybeListener.Expect(() => new Exception($"No device listener found for {identity.Id}"));
            }
            catch (Exception)
            {
                Events.MissingListener(identity.Id);
                return false;
            }

            var message = new EdgeMessage.Builder(publishInfo.Payload).Build();
            message.Properties[SystemProperties.CorrelationId] = rid.Value;
            message.Properties[SystemProperties.StatusCode] = res.Value;

            await listener.ProcessMethodResponseAsync(message);

            return true;
        }

        static string GetMethodCallTopic(IIdentity identity, string methodName, string correlationId, string topicPrefix)
        {
            switch (identity)
            {
                case IDeviceIdentity deviceIdentity:
                    return string.Format(MethodCallToDeviceTopicTemplate, topicPrefix, deviceIdentity.DeviceId, methodName, correlationId);

                case IModuleIdentity moduleIdentity:
                    return string.Format(MethodCallToModuleTopicTemplate, topicPrefix, moduleIdentity.DeviceId, moduleIdentity.ModuleId, methodName, correlationId);

                default:
                    Events.BadIdentityFormat(identity.Id);
                    throw new Exception($"cannot decode identity {identity.Id}");
            }
        }

        static class Events
        {
            const int IdStart = MqttBridgeEventIds.DirectMethodHandler;
            static readonly ILogger Log = Logger.Factory.CreateLogger<DirectMethodHandler>();

            enum EventIds
            {
                MissingListener = IdStart,
                BadPayloadFormat,
                BadIdentityFormat,
                FailedToSendDirectMethodMessage,
            }

            public static void MissingListener(string id) => Log.LogError((int)EventIds.MissingListener, $"Missing device listener for {id}");
            public static void BadPayloadFormat(Exception e) => Log.LogError((int)EventIds.BadPayloadFormat, e, "Bad payload format: cannot deserialize subscription update");
            public static void BadIdentityFormat(string identity) => Log.LogError((int)EventIds.BadIdentityFormat, $"Bad identity format: {identity}");
            public static void FailedToSendDirectMethodMessage(Exception e) => Log.LogError((int)EventIds.FailedToSendDirectMethodMessage, e, "Failed to send direct method message");
        }
    }
}
