// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client.Common;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using Org.BouncyCastle.Math.EC;

    public class TelemetryHandler : ISubscriber, IMessageConsumer
    {
        const string TelemetryDevice = "$edgehub/+/messages/events/#";
        const string TelemetryModule = "$edgehub/+/modules/+/messages/events/#";

        const string TelemetryPublishPattern = @"^\$edgehub/(?<id1>[^/\+\#]+)(/modules/(?<id2>[^/\+\#]+))?/messages/events/(?<bag>.*)";

        static readonly string[] subscriptions = new[] { TelemetryDevice, TelemetryModule };

        readonly IConnectionRegistry connectionRegistry;

        public IReadOnlyCollection<string> Subscriptions => subscriptions;

        public TelemetryHandler(IConnectionRegistry connectionRegistry) => this.connectionRegistry = connectionRegistry;

        public Task<bool> HandleAsync(MqttPublishInfo publishInfo)
        {
            var match = Regex.Match(publishInfo.Topic, TelemetryPublishPattern);
            if (match.Success)
            {
                return this.HandleTelemetry(match, publishInfo);
            }

            return Task.FromResult(false);
        }

        async Task<bool> HandleTelemetry(Match match, MqttPublishInfo publishInfo)
        {
            var id1 = match.Groups["id1"];
            var id2 = match.Groups["id2"];
            var bag = match.Groups["bag"];

            // id1 and bag is mandatory, id2 is present only for modules
            if (!id1.Success || !bag.Success)
            {
                Events.UnexpectedTelemetryTopic(publishInfo.Topic);
                return false;
            }

            var identity = GetIdentityFromIdParts(id1, id2);
            var proxy = await this.connectionRegistry.GetUpstreamProxyAsync(identity);

            var message = default(IMessage);
            try
            {
                message = ConvertToInternalMessage(publishInfo, id1.Value, id2.Success ? Option.Some(id2.Value) : Option.None<string>(), bag.Value);
            }
            catch (Exception e)
            {
                Events.UnexpectedMessageFormat(e, publishInfo.Topic);
                return false;
            }

            try
            {
                _ = proxy.Expect(() => new Exception($"No upstream proxy found for {identity.Id}")).ProcessDeviceMessageAsync(message);
            }
            catch (Exception)
            {
                Events.MissingProxy(identity.Id);
                return false;
            }

            Events.TelemetryMessage(identity.Id, publishInfo.Payload.Length);

            return true;
        }

        static EdgeMessage ConvertToInternalMessage(MqttPublishInfo publishInfo, string deviceId, Option<string> moduleId, string bag)
        {
            var allProperties = new Dictionary<string, string>();
            if (!string.Equals(bag, string.Empty))
            {
                UrlEncodedDictionarySerializer.Deserialize(bag, 0, allProperties);
            }

            var systemProperties = new Dictionary<string, string>();
            var properties = new Dictionary<string, string>();

            systemProperties[SystemProperties.ConnectionDeviceId] = deviceId;
            moduleId.ForEach(id => systemProperties[SystemProperties.ConnectionModuleId] = id);

            foreach (KeyValuePair<string, string> property in allProperties)
            {
                if (SystemProperties.IncomingSystemPropertiesMap.TryGetValue(property.Key, out string systemPropertyName))
                {
                    systemProperties.Add(systemPropertyName, property.Value);
                }
                else
                {
                    properties.Add(property.Key, property.Value);
                }
            }

            EdgeMessage hubMessage = new EdgeMessage.Builder(publishInfo.Payload)
                .SetProperties(properties)
                .SetSystemProperties(systemProperties)
                .Build();

            return hubMessage;
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
            const int IdStart = MqttBridgeEventIds.TelemetryHandler;
            static readonly ILogger Log = Logger.Factory.CreateLogger<TelemetryHandler>();

            enum EventIds
            {
                TelemetryMessage = IdStart,
                UnexpectedTelemetryTopic,
                MissingProxy,
                UnexpectedMessageFormat,
            }

            public static void TelemetryMessage(string id, int messageLen) => Log.LogDebug((int)EventIds.TelemetryMessage, $"Telemetry message sent by client: [{id}], msg len: [{messageLen}]");
            public static void UnexpectedTelemetryTopic(string topic) => Log.LogWarning((int)EventIds.UnexpectedTelemetryTopic, $"Telemetry-like topic strucure with unexpected format [{topic}]");
            public static void MissingProxy(string id) => Log.LogError((int)EventIds.MissingProxy, $"Missing proxy for [{id}]");
            public static void UnexpectedMessageFormat(Exception e, string topic) => Log.LogError((int)EventIds.UnexpectedMessageFormat, e, $"Cannot decode unexpected telemetry message format. Topic with property bag: [{topic}]");
        }
    }
}
