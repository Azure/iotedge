// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    using System.Collections.Generic;
    using DotNetty.Buffers;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using IPgMessage = Microsoft.Azure.Devices.ProtocolGateway.Messaging.IMessage;

    public class PgMessageConverter : IMessageConverter<IPgMessage>
    {
        public Core.IMessage ToMessage(IPgMessage sourceMessage)
        {
            // TODO - Need to implement the logic to convert sourceMessage.Address to message properties. 
            // Look at IMessageAddressConverter in ProtocolGateway.IotHubClient

            byte[] payloadBytes = sourceMessage.Payload.ToByteArray();

            // TODO - What about the other properties (like sequence number, etc)? Ignoring for now, as they are not used anyways.
            var hubMessage = new MqttMessage(payloadBytes, sourceMessage.Properties);
            return hubMessage;
        }

        public IPgMessage FromMessage(Core.IMessage message)
        {
            IByteBuffer payload = message.Body.ToByteBuffer();
            var pgMessage = new PgMessage(payload, null);
            foreach (KeyValuePair<string, string> property in message.Properties)
            {
                pgMessage.Properties.Add(property);
            }

            // TODO - Need to implement logic to derive Address from message properties

            return pgMessage;
        }
    }
}