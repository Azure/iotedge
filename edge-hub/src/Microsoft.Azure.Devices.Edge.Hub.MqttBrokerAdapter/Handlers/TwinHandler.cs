// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
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

    public class TwinHandler : ITwinHandler, ISubscriber, IMessageConsumer, IMessageProducer
    {
        const string TwinGetDevice = "$edgehub/+/twin/get/#";
        const string TwinGetModule = "$edgehub/+/+/twin/get/#";
        const string TwinUpdateDevice = "$edgehub/+/twin/reported/#";
        const string TwinUpdateModule = "$edgehub/+/+/twin/reported/#";

        const string TwinGetPublishPattern = @"^\$edgehub/(?<id1>[^/\+\#]+)(/(?<id2>[^/\+\#]+))?/twin/get/\?\$rid=(?<rid>.+)";
        const string TwinUpdatePublishPattern = @"^\$edgehub/(?<id1>[^/\+\#]+)(/(?<id2>[^/\+\#]+))?/twin/reported/\?\$rid=(?<rid>.+)"; // FIXME make a single pattern combined the one above, capturing the "verb"? ('get' vs 'reported'?)
        const string SubscriptionChangePattern = @"^\$edgehub/(?<id1>[^/\+\#]+)(/(?<id2>[^/\+\#]+))?/subscriptions$";

        const string TwinSubscriptionForResultsPattern = @"^\$edgehub/(?<id1>[^/\+\#]+)(/(?<id2>[^/\+\#]+))?/twin/res/\#$";
        const string TwinSubscriptionForPatchPattern = @"^\$edgehub/(?<id1>[^/\+\#]+)(/(?<id2>[^/\+\#]+))?/twin/desired/\#$";

        const string TwinResultDevice = "$edgehub/{0}/twin/res/{1}/?$rid={2}";
        const string TwinResultModule = "$edgehub/{0}/{1}/twin/res/{2}/?$rid={3}";

        const string DesiredUpdateDevice = "$edgehub/{0}/twin/desired/?$version={1}";
        const string DesiredUpdateModule = "$edgehub/{0}/{1}/twin/desired/?$version={2}";

        static readonly char[] identitySegmentSeparator = new[] { '/' };
        static readonly string[] subscriptions = new[] { TwinGetDevice, TwinGetModule, TwinUpdateDevice, TwinUpdateModule };

        readonly IConnectionRegistry connectionRegistry;
        IMqttBridgeConnector connector;

        public IReadOnlyCollection<string> Subscriptions => subscriptions;

        public TwinHandler(IConnectionRegistry connectionRegistry) => this.connectionRegistry = connectionRegistry;

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

            match = Regex.Match(publishInfo.Topic, SubscriptionChangePattern);
            if (match.Success)
            {
                return this.HandleSubscriptionChanged(match, publishInfo);
            }

            return Task.FromResult(false);
        }

        public void SetConnector(IMqttBridgeConnector connector) => this.connector = connector;

        public async Task SendTwinUpdate(IMessage twin, IIdentity identity)
        {
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
                                                    GetTwinResultTopic(identity, statusCode, correlationId),
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

        public async Task SendDesiredPropertiesUpdate(IMessage desiredProperties, IIdentity identity)
        {
            if (!desiredProperties.SystemProperties.TryGetValue(SystemProperties.Version, out var version))
            {
                Events.DesiredPropertiesUpdateIncompete(identity.Id);
                return;
            }

            bool result;
            try
            {
                result = await this.connector.SendAsync(
                                                GetDesiredPropertiesUpdateTopic(identity, version),
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
                        (proxy, rid) =>
                        {
                            _ = proxy.SendGetTwinRequest(rid);
                        },
                        match,
                        publishInfo);
        }

        Task<bool> HandleUpdateReported(Match match, MqttPublishInfo publishInfo)
        {
            return this.HandleUpstreamRequest(
                        (proxy, rid) =>
                        {
                            var message = new EdgeMessage.Builder(publishInfo.Payload).Build();
                            _ = proxy.UpdateReportedPropertiesAsync(message, rid);
                        },
                        match,
                        publishInfo);
        }

        async Task<bool> HandleUpstreamRequest(Action<IDeviceListener, string> action, Match match, MqttPublishInfo publishInfo)
        {
            var id1 = match.Groups["id1"];
            var id2 = match.Groups["id2"];
            var rid = match.Groups["rid"];

            // id1 and rid is mandatory, id2 is present only for modules
            if (!id1.Success || !rid.Success)
            {
                Events.UnexpectedTwinTopic(publishInfo.Topic);
                return false;
            }

            var identity = GetIdentityFromIdParts(id1, id2);
            var proxy = await this.connectionRegistry.GetUpstreamProxyAsync(identity);

            try
            {
                var message = new EdgeMessage.Builder(publishInfo.Payload).Build();
                action(proxy.Expect(() => new Exception($"No upstream proxy found for {identity.Id}")), rid.Value);
            }
            catch (Exception)
            {
                Events.MissingProxy(identity.Id);
                return false;
            }

            return true;
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

            var subscribesTwinRes = false;
            var subscribesTwinPatch = false;

            foreach (var subscription in subscriptionList)
            {
                var subscriptionMatch = Regex.Match(subscription, TwinSubscriptionForResultsPattern);
                subscribesTwinRes = subscribesTwinRes || IsMatchWithIds(subscriptionMatch, id1, id2);

                subscriptionMatch = Regex.Match(subscription, TwinSubscriptionForPatchPattern);
                subscribesTwinPatch = subscribesTwinPatch || IsMatchWithIds(subscriptionMatch, id1, id2);
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

            await AddOrRemove(proxy, subscribesTwinRes, DeviceSubscription.TwinResponse);
            await AddOrRemove(proxy, subscribesTwinPatch, DeviceSubscription.DesiredPropertyUpdates);

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

        static string GetTwinResultTopic(IIdentity identity, string statusCode, string correlationId)
        {
            var identityComponents = identity.Id.Split(identitySegmentSeparator, StringSplitOptions.RemoveEmptyEntries);

            switch (identityComponents.Length)
            {
                case 1: return string.Format(TwinResultDevice, identityComponents[0], statusCode, correlationId);
                case 2: return string.Format(TwinResultModule, identityComponents[0], identityComponents[1], statusCode, correlationId);

                default:
                    Events.BadIdentityFormat(identity.Id);
                    throw new Exception($"cannot decode identity {identity.Id}");
            }
        }

        static string GetDesiredPropertiesUpdateTopic(IIdentity identity, string version)
        {
            var identityComponents = identity.Id.Split(identitySegmentSeparator, StringSplitOptions.RemoveEmptyEntries);

            switch (identityComponents.Length)
            {
                case 1: return string.Format(DesiredUpdateDevice, identityComponents[0], version);
                case 2: return string.Format(DesiredUpdateModule, identityComponents[0], identityComponents[1], version);

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
                MissingProxy,
                UnexpectedTwinTopic,
                BadIdentityFormat,
                FailedToSendTwinUpdateMessage,
                FailedToSendDesiredPropertiesUpdateMessage,
                BadPayloadFormat
            }

            public static void TwinUpdate(string id, string statusCode, string correlationId, int messageLen) => Log.LogDebug((int)EventIds.TwinUpdate, $"Twin Update sent to client: [{id}], status: [{statusCode}], rid: [{correlationId}], msg len: [{messageLen}]");
            public static void TwinUpdateFailed(string id, string statusCode, string correlationId, int messageLen) => Log.LogError((int)EventIds.TwinUpdateFailed, $"Failed to send Twin Update to client: [{id}], status: [{statusCode}], rid: [{correlationId}], msg len: [{messageLen}]");
            public static void DesiredPropertiesUpdate(string id, string version, int messageLen) => Log.LogDebug((int)EventIds.DesiredPropertiesUpdate, $"Desired Properties Update sent to client: [{id}], version: [{version}], msg len: [{messageLen}]");
            public static void DesiredPropertiesUpdateFailed(string id, string version, int messageLen) => Log.LogError((int)EventIds.DesiredPropertiesUpdateFailed, $"Failed to send Desired Properties Update to client: [{id}], status: [{version}], msg len: [{messageLen}]");
            public static void TwinUpdateIncompete(string id) => Log.LogError((int)EventIds.TwinUpdateIncompete, $"Failed to send Twin Update to client [{id}] because the message is incomplete - not all system properties are present");
            public static void DesiredPropertiesUpdateIncompete(string id) => Log.LogError((int)EventIds.DesiredPropertiesUpdateIncompete, $"Failed to send Desired Properties Update to client [{id}] because the message is incomplete - not all system properties are present");
            public static void MissingProxy(string id) => Log.LogError((int)EventIds.MissingProxy, $"Missing proxy for [{id}]");
            public static void UnexpectedTwinTopic(string topic) => Log.LogWarning((int)EventIds.UnexpectedTwinTopic, $"Twin-like topic strucure with unexpected format [{topic}]");
            public static void BadIdentityFormat(string identity) => Log.LogError((int)EventIds.BadIdentityFormat, $"Bad identity format: {identity}");
            public static void FailedToSendTwinUpdateMessage(Exception e) => Log.LogError((int)EventIds.FailedToSendTwinUpdateMessage, e, "Failed to send twin update message");
            public static void FailedToSendDesiredPropertiesUpdateMessage(Exception e) => Log.LogError((int)EventIds.FailedToSendDesiredPropertiesUpdateMessage, e, "Failed to send Desired Properties Update message");
            public static void BadPayloadFormat(Exception e) => Log.LogError((int)EventIds.BadPayloadFormat, e, "Bad payload format: cannot deserialize subscription update");
        }
    }
}
