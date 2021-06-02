// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using System.Threading.Channels;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class TwinHandler : ITwinHandler, IMessageConsumer, IMessageProducer
    {
        const string TwinGetDirectDevice = "$edgehub/+/twin/get/#";
        const string TwinGetDirectModule = "$edgehub/+/+/twin/get/#";
        const string TwinUpdateDirectDevice = "$edgehub/+/twin/reported/#";
        const string TwinUpdateDirectModule = "$edgehub/+/+/twin/reported/#";
        const string TwinGetIndirectDevice = "$iothub/+/twin/get/#";
        const string TwinGetIndirectModule = "$iothub/+/+/twin/get/#";
        const string TwinUpdateIndirectDevice = "$iothub/+/twin/reported/#";
        const string TwinUpdateIndirectModule = "$iothub/+/+/twin/reported/#";

        const string TwinGetPublishPattern = @"^((?<dialect>(\$edgehub)|(\$iothub)))/(?<id1>[^/\+\#]+)(/(?<id2>[^/\+\#]+))?/twin/get/\?\$rid=(?<rid>.+)";
        const string TwinUpdatePublishPattern = @"^((?<dialect>(\$edgehub)|(\$iothub)))/(?<id1>[^/\+\#]+)(/(?<id2>[^/\+\#]+))?/twin/reported/\?\$rid=(?<rid>.+)";

        const string TwinSubscriptionForResultsPattern = @"^((?<dialect>(\$edgehub)|(\$iothub)))/(?<id1>[^/\+\#]+)(/(?<id2>[^/\+\#]+))?/twin/res/\#$";
        const string TwinSubscriptionForPatchPattern = @"^((?<dialect>(\$edgehub)|(\$iothub)))/(?<id1>[^/\+\#]+)(/(?<id2>[^/\+\#]+))?/twin/desired/\#$";

        const string TwinResultDevice = "{0}/{1}/twin/res/{2}/?$rid={3}";
        const string TwinResultModule = "{0}/{1}/{2}/twin/res/{3}/?$rid={4}";

        const string DesiredUpdateDevice = "{0}/{1}/twin/desired/?$version={2}";
        const string DesiredUpdateModule = "{0}/{1}/{2}/twin/desired/?$version={3}";

        static readonly string[] subscriptions = new[]
                                                 {
                                                    TwinGetDirectDevice, TwinGetDirectModule, TwinUpdateDirectDevice, TwinUpdateDirectModule,
                                                    TwinGetIndirectDevice, TwinGetIndirectModule, TwinUpdateIndirectDevice, TwinUpdateIndirectModule
                                                 };

        static readonly SubscriptionPattern[] subscriptionPatterns = new SubscriptionPattern[]
                                                                         {
                                                                            new SubscriptionPattern(TwinSubscriptionForResultsPattern, DeviceSubscription.TwinResponse),
                                                                            new SubscriptionPattern(TwinSubscriptionForPatchPattern, DeviceSubscription.DesiredPropertyUpdates)
                                                                         };
        readonly IConnectionRegistry connectionRegistry;
        readonly IIdentityProvider identityProvider;

        IMqttBrokerConnector connector;

        public IReadOnlyCollection<string> Subscriptions => subscriptions;

        public TwinHandler(IConnectionRegistry connectionRegistry, IIdentityProvider identityProvider)
        {
            this.connectionRegistry = Preconditions.CheckNotNull(connectionRegistry);
            this.identityProvider = Preconditions.CheckNotNull(identityProvider);
        }

        public IReadOnlyCollection<SubscriptionPattern> WatchedSubscriptions => subscriptionPatterns;

        public Task<bool> HandleAsync(MqttPublishInfo publishInfo)
        {
            var match = Regex.Match(publishInfo.Topic, TwinGetPublishPattern);
            if (match.Success)
            {
                return this.HandleTwinGet(match, publishInfo);
            }

            match = Regex.Match(publishInfo.Topic, TwinUpdatePublishPattern);
            if (match.Success)
            {
                return this.HandleUpdateReported(match, publishInfo);
            }

            return Task.FromResult(false);
        }

        public void SetConnector(IMqttBrokerConnector connector) => this.connector = connector;

        public async Task SendTwinUpdate(IMessage twin, IIdentity identity, bool isDirectClient)
        {
            var topicPrefix = isDirectClient ? MqttBrokerAdapterConstants.DirectTopicPrefix : MqttBrokerAdapterConstants.IndirectTopicPrefix;
            var statusCode = string.Empty;
            var correlationId = string.Empty;

            var allPropertiesPresent = true;

            allPropertiesPresent = allPropertiesPresent && twin.SystemProperties.TryGetValue(SystemProperties.StatusCode, out statusCode);
            allPropertiesPresent = allPropertiesPresent && twin.SystemProperties.TryGetValue(SystemProperties.CorrelationId, out correlationId);

            if (allPropertiesPresent)
            {
                bool result;
                try
                {
                    result = await this.connector.SendAsync(
                                                    GetTwinResultTopic(identity, statusCode, correlationId, topicPrefix),
                                                    twin.Body);
                }
                catch (Exception e)
                {
                    Events.FailedToSendTwinUpdateMessage(e);
                    result = false;
                }

                if (result)
                {
                    Events.TwinUpdate(identity.Id, statusCode, correlationId, twin.Body.Length);
                }
                else
                {
                    Events.TwinUpdateFailed(identity.Id, statusCode, correlationId, twin.Body.Length);
                }
            }
            else
            {
                Events.TwinUpdateIncompete(identity.Id);
            }
        }

        public async Task SendDesiredPropertiesUpdate(IMessage desiredProperties, IIdentity identity, bool isDirectClient)
        {
            var topicPrefix = isDirectClient ? MqttBrokerAdapterConstants.DirectTopicPrefix : MqttBrokerAdapterConstants.IndirectTopicPrefix;

            if (!desiredProperties.SystemProperties.TryGetValue(SystemProperties.Version, out var version))
            {
                Events.DesiredPropertiesUpdateIncompete(identity.Id);
                return;
            }

            bool result;
            try
            {
                result = await this.connector.SendAsync(
                                                GetDesiredPropertiesUpdateTopic(identity, version, topicPrefix),
                                                desiredProperties.Body);
            }
            catch (Exception e)
            {
                Events.FailedToSendDesiredPropertiesUpdateMessage(e);
                result = false;
            }

            if (result)
            {
                Events.DesiredPropertiesUpdate(identity.Id, version, desiredProperties.Body.Length);
            }
            else
            {
                Events.DesiredPropertiesUpdateFailed(identity.Id, version, desiredProperties.Body.Length);
            }
        }

        Task<bool> HandleTwinGet(Match match, MqttPublishInfo publishInfo)
        {
            return this.HandleUpstreamRequest(
                        async (listener, rid) =>
                        {
                            await listener.SendGetTwinRequest(rid);
                        },
                        match,
                        publishInfo);
        }

        Task<bool> HandleUpdateReported(Match match, MqttPublishInfo publishInfo)
        {
            return this.HandleUpstreamRequest(
                        async (listener, rid) =>
                        {
                            var message = new EdgeMessage.Builder(publishInfo.Payload).Build();
                            await listener.UpdateReportedPropertiesAsync(message, rid);
                        },
                        match,
                        publishInfo);
        }

        async Task<bool> HandleUpstreamRequest(Func<IDeviceListener, string, Task> action, Match match, MqttPublishInfo publishInfo)
        {
            var id1 = match.Groups["id1"];
            var id2 = match.Groups["id2"];
            var rid = match.Groups["rid"];

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
            _ = action(listener, rid.Value);

            return true;
        }

        static string GetTwinResultTopic(IIdentity identity, string statusCode, string correlationId, string topicPrefix)
        {
            switch (identity)
            {
                case IModuleIdentity moduleIdentity:
                    return string.Format(TwinResultModule, topicPrefix, moduleIdentity.DeviceId, moduleIdentity.ModuleId, statusCode, correlationId);

                case IDeviceIdentity deviceIdentity:
                    return string.Format(TwinResultDevice, topicPrefix, deviceIdentity.DeviceId, statusCode, correlationId);

                default:
                    Events.BadIdentityFormat(identity.Id);
                    throw new Exception($"cannot decode identity {identity.Id}");
            }
        }

        static string GetDesiredPropertiesUpdateTopic(IIdentity identity, string version, string topicPrefix)
        {
            switch (identity)
            {
                case IModuleIdentity moduleIdentity:
                    return string.Format(DesiredUpdateModule, topicPrefix, moduleIdentity.DeviceId, moduleIdentity.ModuleId, version);

                case IDeviceIdentity deviceIdentity:
                    return string.Format(DesiredUpdateDevice, topicPrefix, deviceIdentity.DeviceId, version);

                default:
                    Events.BadIdentityFormat(identity.Id);
                    throw new Exception($"cannot decode identity {identity.Id}");
            }
        }

        static class Events
        {
            const int IdStart = MqttBridgeEventIds.TwinHandler;
            static readonly ILogger Log = Logger.Factory.CreateLogger<TwinHandler>();

            enum EventIds
            {
                TwinUpdate = IdStart,
                TwinUpdateFailed,
                TwinUpdateIncompete,
                DesiredPropertiesUpdate,
                DesiredPropertiesUpdateFailed,
                DesiredPropertiesUpdateIncompete,
                MissingListener,
                UnexpectedTwinTopic,
                BadIdentityFormat,
                FailedToSendTwinUpdateMessage,
                FailedToSendDesiredPropertiesUpdateMessage,
                BadPayloadFormat
            }

            public static void TwinUpdate(string id, string statusCode, string correlationId, int messageLen) => Log.LogDebug((int)EventIds.TwinUpdate, $"Twin Update sent to client: {id}, status: {statusCode}, rid: {correlationId}, msg len: {messageLen}");
            public static void TwinUpdateFailed(string id, string statusCode, string correlationId, int messageLen) => Log.LogError((int)EventIds.TwinUpdateFailed, $"Failed to send Twin Update to client: {id}, status: {statusCode}, rid: {correlationId}, msg len: {messageLen}");
            public static void DesiredPropertiesUpdate(string id, string version, int messageLen) => Log.LogDebug((int)EventIds.DesiredPropertiesUpdate, $"Desired Properties Update sent to client: {id}, version: {version}, msg len: {messageLen}");
            public static void DesiredPropertiesUpdateFailed(string id, string version, int messageLen) => Log.LogError((int)EventIds.DesiredPropertiesUpdateFailed, $"Failed to send Desired Properties Update to client: {id}, status: {version}, msg len: {messageLen}");
            public static void TwinUpdateIncompete(string id) => Log.LogError((int)EventIds.TwinUpdateIncompete, $"Failed to send Twin Update to client {id} because the message is incomplete - not all system properties are present");
            public static void DesiredPropertiesUpdateIncompete(string id) => Log.LogError((int)EventIds.DesiredPropertiesUpdateIncompete, $"Failed to send Desired Properties Update to client {id} because the message is incomplete - not all system properties are present");
            public static void MissingListener(string id) => Log.LogError((int)EventIds.MissingListener, $"Missing device listener for {id}");
            public static void UnexpectedTwinTopic(string topic) => Log.LogWarning((int)EventIds.UnexpectedTwinTopic, $"Twin-like topic strucure with unexpected format {topic}");
            public static void BadIdentityFormat(string identity) => Log.LogError((int)EventIds.BadIdentityFormat, $"Bad identity format: {identity}");
            public static void FailedToSendTwinUpdateMessage(Exception e) => Log.LogError((int)EventIds.FailedToSendTwinUpdateMessage, e, "Failed to send twin update message");
            public static void FailedToSendDesiredPropertiesUpdateMessage(Exception e) => Log.LogError((int)EventIds.FailedToSendDesiredPropertiesUpdateMessage, e, "Failed to send Desired Properties Update message");
            public static void BadPayloadFormat(Exception e) => Log.LogError((int)EventIds.BadPayloadFormat, e, "Bad payload format: cannot deserialize subscription update");
        }
    }
}
