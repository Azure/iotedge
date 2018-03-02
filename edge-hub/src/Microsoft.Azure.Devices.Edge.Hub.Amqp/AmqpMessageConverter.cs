// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Microsoft.Azure.Amqp;
    using Microsoft.Azure.Devices.Edge.Hub.Core;

    public class AmqpMessageConverter : IMessageConverter<AmqpMessage>
    {
        public IMessage ToMessage(AmqpMessage sourceMessage)
        {
            byte[] GetMessageBody()
            {
                using (var ms = new MemoryStream())
                {
                    sourceMessage.BodyStream.CopyTo(ms);
                    return ms.ToArray();
                }
            }

            IDictionary<string, string> properties = sourceMessage.ApplicationProperties.Map.ToDictionary(v => v.Key.ToString(), v => v.Value as string);

            // TODO: Figure out all the system properties that need to be set.
            var systemProperties = new Dictionary<string, string>();
            if (!string.IsNullOrWhiteSpace(sourceMessage.Properties.ContentEncoding.Value))
            {
                systemProperties[SystemProperties.ContentEncoding] = sourceMessage.Properties.ContentEncoding.Value;
            }

            if (!string.IsNullOrWhiteSpace(sourceMessage.Properties.ContentType.Value))
            {
                systemProperties[SystemProperties.ContentType] = sourceMessage.Properties.ContentType.Value;
            }

            if (sourceMessage.Properties.CorrelationId != null)
            {
                systemProperties[SystemProperties.ContentEncoding] = sourceMessage.Properties.CorrelationId.ToString();
            }

            if (sourceMessage.Properties.MessageId != null)
            {
                systemProperties[SystemProperties.MessageId] = sourceMessage.Properties.MessageId.ToString();

            }

            return new EdgeMessage(GetMessageBody(), properties, systemProperties);
        }

        public AmqpMessage FromMessage(IMessage message)
        {
            throw new System.NotImplementedException();
        }
    }
}
