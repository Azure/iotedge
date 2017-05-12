// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using DotNetty.Buffers;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using IProtocolGatewayMessage = Microsoft.Azure.Devices.ProtocolGateway.Messaging.IMessage;

    public class ProtocolGatewayMessageConverter : IMessageConverter<IProtocolGatewayMessage>
    {
        readonly MessageAddressConverter addressConvertor;

        public ProtocolGatewayMessageConverter(MessageAddressConverter addressConvertor)
        {
            this.addressConvertor = addressConvertor;
        }

        public Core.IMessage ToMessage(IProtocolGatewayMessage sourceMessage)
        {
            // TODO: should reject messages which are not matched ( PassThroughUnmatchedMessages)
            this.addressConvertor.TryParseAddressIntoMessageProperties(sourceMessage.Address, sourceMessage);

            byte[] payloadBytes = sourceMessage.Payload.ToByteArray();

            // TODO - What about the other properties (like sequence number, etc)? Ignoring for now, as they are not used anyways.
            MqttMessage hubMessage = new MqttMessage.Builder(payloadBytes).SetProperties(sourceMessage.Properties).Build();
            return hubMessage;
        }

        public IProtocolGatewayMessage FromMessage(Core.IMessage message)
        {
            string lockToken;
            string createdTime;
            DateTime createdTimeUtc = DateTime.MinValue;
            message.SystemProperties.TryGetValue(Core.SystemProperties.LockToken, out lockToken);
            if (message.SystemProperties.TryGetValue(Core.SystemProperties.EnqueuedTime, out createdTime))
            {
                createdTimeUtc = DateTime.Parse(createdTime);
            }

            this.addressConvertor.TryDeriveAddress(message.SystemProperties.ToDictionary(kvp => kvp.Key, kvp => kvp.Value), out string address);

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