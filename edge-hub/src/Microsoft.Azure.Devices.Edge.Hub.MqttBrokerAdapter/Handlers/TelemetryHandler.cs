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

    public class TelemetryHandler : ISubscriber, IMessageConsumer
    {
        const string TelemetryDevice = "$edgehub/+/messages/events/#";
        const string TelemetryModule = "$edgehub/+/modules/+/messages/events/#";

        const string TelemetryPublishPattern = @"^\$edgehub/(?<id1>[^/\+\#]+)(/modules/(?<id2>[^/\+\#]+))?/messages/events(/(?<bag>.*))?";

        static readonly string[] subscriptions = new[] { TelemetryDevice, TelemetryModule };

        readonly IConnectionRegistry connectionRegistry;
        readonly IIdentityProvider identityProvider;

        readonly Regex telemetryPublishRegex = new Regex(TelemetryPublishPattern, RegexOptions.Compiled);

        public IReadOnlyCollection<string> Subscriptions => subscriptions;

        public TelemetryHandler(IConnectionRegistry connectionRegistry, IIdentityProvider identityProvider)
        {
            this.connectionRegistry = connectionRegistry;
            this.identityProvider = identityProvider;
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

        public void ProducerStopped()
        {
        }

        async Task<bool> HandleTelemetry(Match match, MqttPublishInfo publishInfo)
        {
            var id1 = match.Groups["id1"];
            var id2 = match.Groups["id2"];
            var bag = match.Groups["bag"];

            var identity = id2.Success
                                ? this.identityProvider.Create(id1.Value, id2.Value)
                                : this.identityProvider.Create(id1.Value);

            var maybeProxy = await this.connectionRegistry.GetUpstreamProxyAsync(identity);
            var proxy = default(IDeviceListener);

            try
            {
                proxy = maybeProxy.Expect(() => new Exception($"No upstream proxy found for {identity.Id}"));
            }
            catch (Exception)
            {
                Events.MissingProxy(identity.Id);
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

            await proxy.ProcessDeviceMessageAsync(message);

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
