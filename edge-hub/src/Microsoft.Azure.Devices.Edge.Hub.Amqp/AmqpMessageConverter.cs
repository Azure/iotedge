// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using Microsoft.Azure.Amqp;
    using Microsoft.Azure.Amqp.Encoding;
    using Microsoft.Azure.Amqp.Framing;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Util;

    /// <summary>
    /// This converter contains the logic to convert telemetry messages to/from amqp messages
    /// </summary>
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

            var systemProperties = new Dictionary<string, string>();
            var properties = new Dictionary<string, string>();

            systemProperties.AddIfNonEmpty(SystemProperties.MessageId, sourceMessage.Properties.MessageId?.ToString());
            systemProperties.AddIfNonEmpty(SystemProperties.MsgCorrelationId, sourceMessage.Properties.CorrelationId?.ToString());
            systemProperties.AddIfNonEmpty(SystemProperties.ContentType, sourceMessage.Properties.ContentType.Value);
            systemProperties.AddIfNonEmpty(SystemProperties.ContentEncoding, sourceMessage.Properties.ContentEncoding.Value);
            systemProperties.AddIfNonEmpty(SystemProperties.To, sourceMessage.Properties.To?.ToString());
            systemProperties.AddIfNonEmpty(SystemProperties.UserId, sourceMessage.Properties.UserId.Count > 0 ? Encoding.UTF8.GetString(sourceMessage.Properties.UserId.Array) : null);
            systemProperties.AddIfNonEmpty(SystemProperties.ExpiryTimeUtc, sourceMessage.Properties.AbsoluteExpiryTime?.ToString("o"));

            if (sourceMessage.MessageAnnotations.Map.TryGetValue(Constants.MessageAnnotationsEnqueuedTimeKey, out DateTime enqueuedTime))
            {
                systemProperties[SystemProperties.EnqueuedTime] = enqueuedTime.ToString("o");
            }

            if (sourceMessage.MessageAnnotations.Map.TryGetValue(Constants.MessageAnnotationsDeliveryCountKey, out byte deliveryCount))
            {
                systemProperties[SystemProperties.DeliveryCount] = deliveryCount.ToString();
            }

            if (sourceMessage.MessageAnnotations.Map.TryGetValue(Constants.MessageAnnotationsSequenceNumberName, out ulong sequenceNumber) && sequenceNumber > 0)
            {
                systemProperties[SystemProperties.SequenceNumber] = sequenceNumber.ToString();
            }

            if (sourceMessage.MessageAnnotations.Map.TryGetValue(Constants.MessageAnnotationsLockTokenName, out string lockToken))
            {
                systemProperties.AddIfNonEmpty(SystemProperties.LockToken, lockToken);
            }

            if (sourceMessage.ApplicationProperties != null)
            {
                foreach (KeyValuePair<MapKey, object> property in sourceMessage.ApplicationProperties.Map)
                {
                    string key = property.Key.ToString();
                    string value = property.Value as string;
                    switch (key)
                    {
                        case Constants.MessagePropertiesMessageSchemaKey:
                            systemProperties[SystemProperties.MessageSchema] = value;
                            break;

                        case Constants.MessagePropertiesCreationTimeKey:
                            systemProperties[SystemProperties.CreationTime] = value;
                            break;

                        case Constants.MessagePropertiesOperationKey:
                            systemProperties[SystemProperties.Operation] = value;
                            break;

                        case Constants.MessagePropertiesOutputNameKey:
                            systemProperties[SystemProperties.OutputName] = value;
                            break;

                        default:
                            properties[key] = value;
                            break;
                    }
                }
            }

            return new EdgeMessage(GetMessageBody(), properties, systemProperties);
        }

        public AmqpMessage FromMessage(IMessage message)
        {
            AmqpMessage amqpMessage = AmqpMessage.Create(
                new Data
                {
                    Value = new ArraySegment<byte>(message.Body)
                });

            if (message.SystemProperties.TryGetNonEmptyValue(SystemProperties.MessageId, out string messageId))
            {
                amqpMessage.Properties.MessageId = messageId;
            }

            if (message.SystemProperties.TryGetNonEmptyValue(SystemProperties.To, out string to))
            {
                amqpMessage.Properties.To = to;
            }

            if (message.SystemProperties.TryGetNonEmptyValue(SystemProperties.ExpiryTimeUtc, out string expiryTimeStr) &&
                DateTime.TryParse(expiryTimeStr, null, DateTimeStyles.RoundtripKind, out DateTime expiryTime))
            {
                amqpMessage.Properties.AbsoluteExpiryTime = expiryTime;
            }

            if (message.SystemProperties.TryGetNonEmptyValue(SystemProperties.MsgCorrelationId, out string correlationId))
            {
                amqpMessage.Properties.CorrelationId = correlationId;
            }

            if (message.SystemProperties.TryGetNonEmptyValue(SystemProperties.UserId, out string userId))
            {
                amqpMessage.Properties.UserId = new ArraySegment<byte>(Encoding.UTF8.GetBytes(userId));
            }

            if (message.SystemProperties.TryGetNonEmptyValue(SystemProperties.ContentType, out string contentType))
            {
                amqpMessage.Properties.ContentType = contentType;
            }

            if (message.SystemProperties.TryGetNonEmptyValue(SystemProperties.ContentEncoding, out string contentEncoding))
            {
                amqpMessage.Properties.ContentEncoding = contentEncoding;
            }

            if (message.SystemProperties.TryGetNonEmptyValue(SystemProperties.EnqueuedTime, out string enqueuedTimeString)
                && DateTime.TryParse(enqueuedTimeString, null, DateTimeStyles.RoundtripKind, out DateTime enqueuedTime))
            {
                amqpMessage.MessageAnnotations.Map[Constants.MessageAnnotationsEnqueuedTimeKey] = enqueuedTime;
            }

            if (message.SystemProperties.TryGetNonEmptyValue(SystemProperties.DeliveryCount, out string deliveryCountString)
                && byte.TryParse(deliveryCountString, out byte deliveryCount))
            {
                amqpMessage.MessageAnnotations.Map[Constants.MessageAnnotationsDeliveryCountKey] = deliveryCount;
            }

            if (message.SystemProperties.TryGetNonEmptyValue(SystemProperties.LockToken, out string lockToken))
            {
                amqpMessage.MessageAnnotations.Map[Constants.MessageAnnotationsLockTokenName] = lockToken;
            }

            if (message.SystemProperties.TryGetNonEmptyValue(SystemProperties.SequenceNumber, out string sequenceNumberString)
                && ulong.TryParse(sequenceNumberString, out ulong sequenceNumber))
            {
                amqpMessage.MessageAnnotations.Map[Constants.MessageAnnotationsSequenceNumberName] = sequenceNumber;
            }

            if (message.SystemProperties.TryGetNonEmptyValue(SystemProperties.InputName, out string inputName))
            {
                amqpMessage.MessageAnnotations.Map[Constants.MessageAnnotationsInputNameKey] = inputName;
            }

            if (message.SystemProperties.TryGetNonEmptyValue(SystemProperties.ConnectionDeviceId, out string connectionDeviceId))
            {
                amqpMessage.MessageAnnotations.Map[Constants.MessageAnnotationsConnectionDeviceId] = connectionDeviceId;
            }

            if (message.SystemProperties.TryGetNonEmptyValue(SystemProperties.ConnectionModuleId, out string connectionModuleId))
            {
                amqpMessage.MessageAnnotations.Map[Constants.MessageAnnotationsConnectionModuleId] = connectionModuleId;
            }

            if (message.SystemProperties.TryGetNonEmptyValue(SystemProperties.MessageSchema, out string messageSchema))
            {
                amqpMessage.ApplicationProperties.Map[Constants.MessagePropertiesMessageSchemaKey] = messageSchema;
            }

            if (message.SystemProperties.TryGetNonEmptyValue(SystemProperties.CreationTime, out string creationTime))
            {
                amqpMessage.ApplicationProperties.Map[Constants.MessagePropertiesCreationTimeKey] = creationTime;
            }

            if (message.SystemProperties.TryGetNonEmptyValue(SystemProperties.Operation, out string operation))
            {
                amqpMessage.ApplicationProperties.Map[Constants.MessagePropertiesOperationKey] = operation;
            }

            foreach (KeyValuePair<string, string> property in message.Properties)
            {
                amqpMessage.ApplicationProperties.Map[property.Key] = property.Value;
            }

            amqpMessage.DeliveryTag = !string.IsNullOrWhiteSpace(lockToken) && Guid.TryParse(lockToken, out Guid lockTokenGuid)
                ? new ArraySegment<byte>(lockTokenGuid.ToByteArray())
                : new ArraySegment<byte>(Guid.NewGuid().ToByteArray());

            return amqpMessage;
        }
    }
}
