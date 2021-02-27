// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client.Common;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class TelemetryHandler : IMessageConsumer
    {
        const string TelemetryDirectDevice = "$edgehub/+/messages/events/#";
        const string TelemetryDirectModule = "$edgehub/+/+/messages/events/#";
        const string TelemetryIndirectDevice = "$iothub/+/messages/events/#";
        const string TelemetryIndirectModule = "$iothub/+/+/messages/events/#";

        const string TelemetryPublishPattern = @"^((?<dialect>(\$edgehub)|(\$iothub)))/(?<id1>[^/\+\#]+)(/(?<id2>[^/\+\#]+))?/messages/events(/(?<bag>.*))?";

        static readonly string[] subscriptions = new[] { TelemetryDirectDevice, TelemetryDirectModule, TelemetryIndirectDevice, TelemetryIndirectModule };

        readonly IConnectionRegistry connectionRegistry;
        readonly IIdentityProvider identityProvider;

        readonly Regex telemetryPublishRegex = new Regex(TelemetryPublishPattern, RegexOptions.Compiled);

        public IReadOnlyCollection<string> Subscriptions => subscriptions;

        public TelemetryHandler(IConnectionRegistry connectionRegistry, IIdentityProvider identityProvider)
        {
            this.connectionRegistry = Preconditions.CheckNotNull(connectionRegistry);
            this.identityProvider = Preconditions.CheckNotNull(identityProvider);
        }

        public Task<bool> HandleAsync(MqttPublishInfo publishInfo)
        {
            var match = this.telemetryPublishRegex.Match(publishInfo.Topic);
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

            await listener.ProcessDeviceMessageAsync(message);

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

            var hubMessage = new EdgeMessage.Builder(publishInfo.Payload)
                                        .SetProperties(properties)
                                        .SetSystemProperties(systemProperties)
                                        .Build();

            return hubMessage;
        }

        static class Events
        {
            const int IdStart = MqttBridgeEventIds.TelemetryHandler;
            static readonly ILogger Log = Logger.Factory.CreateLogger<TelemetryHandler>();

            enum EventIds
            {
                TelemetryMessage = IdStart,
                UnexpectedTelemetryTopic,
                MissingListener,
                UnexpectedMessageFormat,
            }

            public static void TelemetryMessage(string id, int messageLen) => Log.LogDebug((int)EventIds.TelemetryMessage, $"Telemetry message sent by client: {id}, msg len: {messageLen}");
            public static void UnexpectedTelemetryTopic(string topic) => Log.LogWarning((int)EventIds.UnexpectedTelemetryTopic, $"Telemetry-like topic strucure with unexpected format {topic}");
            public static void MissingListener(string id) => Log.LogError((int)EventIds.MissingListener, $"Missing device listener for [{id}]");
            public static void UnexpectedMessageFormat(Exception e, string topic) => Log.LogError((int)EventIds.UnexpectedMessageFormat, e, $"Cannot decode unexpected telemetry message format. Topic with property bag: {topic}");
        }
    }
}
