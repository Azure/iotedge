// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using DotNetty.Buffers;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt;
    using IProtocolGatewayMessage = Microsoft.Azure.Devices.ProtocolGateway.Messaging.IMessage;

    public class ProtocolGatewayMessageConverter : IMessageConverter<IProtocolGatewayMessage>
    {
        readonly MessageAddressConverter addressConvertor;
        readonly IByteBufferConverter byteBufferConverter;

        public ProtocolGatewayMessageConverter(MessageAddressConverter addressConvertor, IByteBufferConverter byteBufferConverter)
        {
            this.addressConvertor = Preconditions.CheckNotNull(addressConvertor, nameof(addressConvertor));
            this.byteBufferConverter = Preconditions.CheckNotNull(byteBufferConverter, nameof(byteBufferConverter));
        }

        public IMessage ToMessage(IProtocolGatewayMessage sourceMessage)
        {
            if (!this.addressConvertor.TryParseProtocolMessagePropsFromAddress(sourceMessage))
            {
                throw new InvalidOperationException("Topic name could not be matched against any of the configured routes.");
            }

            byte[] payloadBytes = this.byteBufferConverter.ToByteArray(sourceMessage.Payload);

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

            EdgeMessage hubMessage = new EdgeMessage.Builder(payloadBytes)
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
                createdTimeUtc = DateTime.Parse(createdTime, null, DateTimeStyles.RoundtripKind);
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

            foreach (KeyValuePair<string, string> systemProperty in message.SystemProperties)
            {
                if (SystemProperties.OutgoingSystemPropertiesMap.TryGetValue(systemProperty.Key, out string onWirePropertyName))
                {
                    properties[onWirePropertyName] = systemProperty.Value;
                }
            }

            if (!this.addressConvertor.TryBuildProtocolAddressFromEdgeHubMessage(uriTemplateKey, message, properties, out string address))
            {
                throw new InvalidOperationException("Could not derive destination address using message system properties");
            }

            IByteBuffer payload = this.byteBufferConverter.ToByteBuffer(message.Body);
            ProtocolGatewayMessage pgMessage = new ProtocolGatewayMessage.Builder(payload, address)
                .WithId(lockToken)
                .WithCreatedTimeUtc(createdTimeUtc)
                .WithProperties(properties)
                .Build();

            return pgMessage;
        }
    }
}
