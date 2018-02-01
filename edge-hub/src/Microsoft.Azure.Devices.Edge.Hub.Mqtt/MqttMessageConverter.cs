// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Util;

    public class MqttMessageConverter : IMessageConverter<Message>
    {
        // Same Value as IotHub
        static readonly TimeSpan ClockSkewAdjustment = TimeSpan.FromSeconds(30);

        public Message FromMessage(IMessage inputMessage)
        {
            Preconditions.CheckNotNull(inputMessage, nameof(inputMessage));
            Preconditions.CheckArgument(inputMessage.Body != null, "IMessage.Body should not be null");

            var message = new Message(inputMessage.Body);

            if (inputMessage.Properties != null)
            {
                foreach (KeyValuePair<string, string> inputMessageProperty in inputMessage.Properties)
                {
                    message.Properties.Add(inputMessageProperty);
                }
            }

            if (inputMessage.SystemProperties != null)
            {
                if (inputMessage.SystemProperties.TryGetValue(SystemProperties.MessageId, out string messageId))
                {
                    message.MessageId = messageId;
                }

                if (inputMessage.SystemProperties.TryGetValue(SystemProperties.MsgCorrelationId, out string correlationId))
                {
                    message.CorrelationId = correlationId;
                }

                if (inputMessage.SystemProperties.TryGetValue(SystemProperties.UserId, out string userId))
                {
                    message.UserId = userId;
                }

                if (inputMessage.SystemProperties.TryGetValue(SystemProperties.ContentType, out string contentType))
                {
                    message.ContentType = contentType;
                }

                if (inputMessage.SystemProperties.TryGetValue(SystemProperties.ContentEncoding, out string contentEncoding))
                {
                    message.ContentEncoding = contentEncoding;
                }

                if (inputMessage.SystemProperties.TryGetValue(SystemProperties.To, out string to))
                {
                    message.To = to;
                }

                if (inputMessage.SystemProperties.TryGetValue(SystemProperties.CreationTime, out string creationTime))
                {
                    message.CreationTimeUtc = DateTime.ParseExact(creationTime, "o", CultureInfo.InvariantCulture);
                }

                if (inputMessage.SystemProperties.TryGetValue(SystemProperties.MessageSchema, out string messageSchema))
                {
                    message.MessageSchema = messageSchema;
                }
            }

            return message;
        }

        public IMessage ToMessage(Message sourceMessage)
        {
            MqttMessage message = new MqttMessage.Builder(sourceMessage.GetBytes())
                .SetProperties(sourceMessage.Properties)
                .Build();

            message.SystemProperties.Add(SystemProperties.MessageId, sourceMessage.MessageId);
            message.SystemProperties.Add(SystemProperties.MsgCorrelationId, sourceMessage.CorrelationId);
            message.SystemProperties.Add(SystemProperties.UserId, sourceMessage.UserId);
            message.SystemProperties.Add(SystemProperties.ContentType, sourceMessage.ContentType);
            message.SystemProperties.Add(SystemProperties.ContentEncoding, sourceMessage.ContentEncoding);
            message.SystemProperties.Add(SystemProperties.To, sourceMessage.To);
            message.SystemProperties.Add(SystemProperties.MessageSchema, sourceMessage.MessageSchema);
            message.SystemProperties.Add(SystemProperties.CreationTime, sourceMessage.CreationTimeUtc.ToString("o"));
            DateTime enqueuedTime = sourceMessage.EnqueuedTimeUtc == DateTime.MinValue ? DateTime.UtcNow : sourceMessage.EnqueuedTimeUtc.Add(ClockSkewAdjustment);
            message.SystemProperties.Add(SystemProperties.EnqueuedTime, enqueuedTime.ToString("o"));
            message.SystemProperties.Add(SystemProperties.LockToken, sourceMessage.LockToken);
            message.SystemProperties.Add(SystemProperties.DeliveryCount, sourceMessage.DeliveryCount.ToString());

            return message;
        }
    }
}
