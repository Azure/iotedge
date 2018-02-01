// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    using System;
    using System.Collections.Generic;
    using DotNetty.Buffers;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt;
    using IProtocolGatewayMessage = Microsoft.Azure.Devices.ProtocolGateway.Messaging.IMessage;

    public class ProtocolGatewayMessageConverter : IMessageConverter<IProtocolGatewayMessage>
    {        
        readonly MessageAddressConverter addressConvertor;

        public ProtocolGatewayMessageConverter(MessageAddressConverter addressConvertor)
        {
            this.addressConvertor = Preconditions.CheckNotNull(addressConvertor, nameof(addressConvertor));
        }

        public Core.IMessage ToMessage(IProtocolGatewayMessage sourceMessage)
        {
            // TODO: should reject messages which are not matched ( PassThroughUnmatchedMessages)
            this.addressConvertor.TryParseProtocolMessagePropsFromAddress(sourceMessage);

            byte[] payloadBytes = sourceMessage.Payload.ToByteArray();

            // TODO - What about the other properties (like sequence number, etc)? Ignoring for now, as they are not used anyways.

            var systemProperties = new Dictionary<string, string>();
            var properties = new Dictionary<string, string>();
            if (sourceMessage.Properties.TryGetValue(TemplateParameters.DeviceIdTemplateParam, out string deviceIdValue))
            {
                systemProperties[SystemProperties.ConnectionDeviceId] = deviceIdValue;
                sourceMessage.Properties.Remove(TemplateParameters.DeviceIdTemplateParam);
            }

            if (sourceMessage.Properties.TryGetValue(Constants.ModuleIdTemplateParameter, out string moduleIdValue))
            {
                systemProperties[SystemProperties.ConnectionModuleId] = moduleIdValue;
                sourceMessage.Properties.Remove(Constants.ModuleIdTemplateParameter);
            }

            foreach (KeyValuePair<string, string> property in sourceMessage.Properties)
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

            MqttMessage hubMessage = new MqttMessage.Builder(payloadBytes)
                .SetProperties(properties)
                .SetSystemProperties(systemProperties)
                .Build();
            return hubMessage;
        }

        public IProtocolGatewayMessage FromMessage(IMessage message)
        {
            message.SystemProperties.TryGetValue(SystemProperties.LockToken, out string lockToken);

            DateTime createdTimeUtc = DateTime.UtcNow;
            if (message.SystemProperties.TryGetValue(SystemProperties.EnqueuedTime, out string createdTime))
            {
                createdTimeUtc = DateTime.Parse(createdTime, null, System.Globalization.DateTimeStyles.RoundtripKind);
            }

            if (!message.SystemProperties.TryGetValue(SystemProperties.OutboundUri, out string uriTemplateKey))
            {
                throw new InvalidOperationException("Could not find key " + SystemProperties.OutboundUri + " in message system properties.");
            }

            IDictionary<string, string> properties = new Dictionary<string, string>();
            foreach (KeyValuePair<string, string> property in message.Properties)
            {
                properties.Add(property);
            }

            foreach(KeyValuePair<string, string> systemProperty in message.SystemProperties)
            {
                if (SystemProperties.OutgoingSystemPropertiesMap.TryGetValue(systemProperty.Key, out string onWirePropertyName))
                {
                    properties.Add(onWirePropertyName, systemProperty.Value);
                }
            }

            if (!this.addressConvertor.TryBuildProtocolAddressFromEdgeHubMessage(uriTemplateKey, message, properties, out string address))
            {
                throw new InvalidOperationException("Could not derive destination address using message system properties");
            }

            IByteBuffer payload = message.Body.ToByteBuffer();
            ProtocolGatewayMessage pgMessage = new ProtocolGatewayMessage.Builder(payload, address)
                .WithId(lockToken)
                .WithCreatedTimeUtc(createdTimeUtc)
                .WithProperties(properties)
                .Build();

            return pgMessage;
        }
    }
}
