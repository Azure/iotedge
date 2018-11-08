// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Util;

    public class DeviceClientMessageConverter : IMessageConverter<Message>
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
                if (inputMessage.SystemProperties.TryGetNonEmptyValue(SystemProperties.MessageId, out string messageId))
                {
                    message.MessageId = messageId;
                }

                if (inputMessage.SystemProperties.TryGetNonEmptyValue(SystemProperties.MsgCorrelationId, out string correlationId))
                {
                    message.CorrelationId = correlationId;
                }

                if (inputMessage.SystemProperties.TryGetNonEmptyValue(SystemProperties.UserId, out string userId))
                {
                    message.UserId = userId;
                }

                if (inputMessage.SystemProperties.TryGetNonEmptyValue(SystemProperties.ContentType, out string contentType))
                {
                    message.ContentType = contentType;
                }

                if (inputMessage.SystemProperties.TryGetNonEmptyValue(SystemProperties.ContentEncoding, out string contentEncoding))
                {
                    message.ContentEncoding = contentEncoding;
                }

                if (inputMessage.SystemProperties.TryGetNonEmptyValue(SystemProperties.To, out string to))
                {
                    message.To = to;
                }

                if (inputMessage.SystemProperties.TryGetNonEmptyValue(SystemProperties.CreationTime, out string creationTime))
                {
                    message.CreationTimeUtc = DateTime.ParseExact(creationTime, "o", CultureInfo.InvariantCulture);
                }

                if (inputMessage.SystemProperties.TryGetNonEmptyValue(SystemProperties.MessageSchema, out string messageSchema))
                {
                    message.MessageSchema = messageSchema;
                }
            }

            return message;
        }

        public IMessage ToMessage(Message sourceMessage)
        {
            EdgeMessage message = new EdgeMessage.Builder(sourceMessage.GetBytes())
                .SetProperties(sourceMessage.Properties)
                .Build();

            message.SystemProperties.AddIfNonEmpty(SystemProperties.MessageId, sourceMessage.MessageId);
            message.SystemProperties.AddIfNonEmpty(SystemProperties.MsgCorrelationId, sourceMessage.CorrelationId);
            message.SystemProperties.AddIfNonEmpty(SystemProperties.UserId, sourceMessage.UserId);
            message.SystemProperties.AddIfNonEmpty(SystemProperties.ContentType, sourceMessage.ContentType);
            message.SystemProperties.AddIfNonEmpty(SystemProperties.ContentEncoding, sourceMessage.ContentEncoding);
            message.SystemProperties.AddIfNonEmpty(SystemProperties.To, sourceMessage.To);
            message.SystemProperties.AddIfNonEmpty(SystemProperties.MessageSchema, sourceMessage.MessageSchema);
            message.SystemProperties.AddIfNonEmpty(SystemProperties.LockToken, sourceMessage.LockToken);
            message.SystemProperties.AddIfNonEmpty(SystemProperties.DeliveryCount, sourceMessage.DeliveryCount.ToString());

            if (sourceMessage.SequenceNumber > 0)
            {
                message.SystemProperties.AddIfNonEmpty(SystemProperties.SequenceNumber, sourceMessage.SequenceNumber.ToString());
            }

            DateTime enqueuedTime = sourceMessage.EnqueuedTimeUtc == DateTime.MinValue ? DateTime.UtcNow : sourceMessage.EnqueuedTimeUtc.Add(ClockSkewAdjustment);
            message.SystemProperties.Add(SystemProperties.EnqueuedTime, enqueuedTime.ToString("o"));

            if (sourceMessage.ExpiryTimeUtc > DateTime.MinValue)
            {
                message.SystemProperties.Add(SystemProperties.ExpiryTimeUtc, sourceMessage.ExpiryTimeUtc.ToString("o"));
            }

            if (sourceMessage.CreationTimeUtc > DateTime.MinValue)
            {
                message.SystemProperties.Add(SystemProperties.CreationTime, sourceMessage.CreationTimeUtc.ToString("o"));
            }

            return message;
        }
    }
}
