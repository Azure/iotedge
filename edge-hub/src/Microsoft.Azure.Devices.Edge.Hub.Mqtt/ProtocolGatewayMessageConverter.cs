// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using DotNetty.Buffers;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt;
    using Microsoft.Azure.Devices.Edge.Util;
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
            this.addressConvertor.TryParseAddressIntoMessageProperties(sourceMessage.Address, sourceMessage);

            byte[] payloadBytes = sourceMessage.Payload.ToByteArray();

            // TODO - What about the other properties (like sequence number, etc)? Ignoring for now, as they are not used anyways.

            var systemProperties = new Dictionary<string, string>();
            if (sourceMessage.Properties.TryGetValue(TemplateParameters.DeviceIdTemplateParam, out string deviceIdValue))
            {
                systemProperties[SystemProperties.DeviceId] = deviceIdValue;
                sourceMessage.Properties.Remove(SystemProperties.DeviceId);
            }

            if (sourceMessage.Properties.TryGetValue(SystemProperties.ModuleId, out string moduleIdValue))
            {
                systemProperties[SystemProperties.ModuleId] = moduleIdValue;
                sourceMessage.Properties.Remove(SystemProperties.ModuleId);
            }

            if (sourceMessage.Properties.TryGetValue(SystemProperties.EndpointId, out string endpointIdValue))
            {
                systemProperties[SystemProperties.EndpointId] = endpointIdValue;
                sourceMessage.Properties.Remove(SystemProperties.EndpointId);
            }

            MqttMessage hubMessage = new MqttMessage.Builder(payloadBytes)
                .SetProperties(sourceMessage.Properties)
                .SetSystemProperties(systemProperties)
                .Build();
            return hubMessage;
        }

        public IProtocolGatewayMessage FromMessage(Core.IMessage message)
        {
            string lockToken;
            string createdTime;

            message.SystemProperties.TryGetValue(Core.SystemProperties.LockToken, out lockToken);

            DateTime createdTimeUtc = DateTime.UtcNow;
            if (message.SystemProperties.TryGetValue(Core.SystemProperties.EnqueuedTime, out createdTime))
            {
                createdTimeUtc = DateTime.Parse(createdTime);
            }

            if (!message.SystemProperties.TryGetValue(Core.SystemProperties.OutboundURI, out string uriTemplateKey))
            {
                throw new InvalidOperationException("Could not find key " + Core.SystemProperties.OutboundURI + " in message system properties.");
            }

            if (!this.addressConvertor.TryDeriveAddress(uriTemplateKey, message.SystemProperties.ToDictionary(kvp => kvp.Key, kvp => kvp.Value), out string address))
            {
                throw new InvalidOperationException("Could not derive destination address using message system properties");
            }

            IByteBuffer payload = message.Body.ToByteBuffer();

            var pgMessage = new ProtocolGatewayMessage(payload, address, new Dictionary<string, string>(), lockToken, createdTimeUtc, 0, 0);
            foreach (KeyValuePair<string, string> property in message.Properties)
            {
                pgMessage.Properties.Add(property);
            }
            return pgMessage;
        }
    }
}
