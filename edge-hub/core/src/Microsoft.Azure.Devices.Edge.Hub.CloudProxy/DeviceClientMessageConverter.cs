// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Util;

    /// <summary>
    /// Converts between internal IMessage and SDK v2 TelemetryMessage (outgoing) / IncomingMessage (incoming).
    /// Implements IMessageConverter for both TelemetryMessage and IncomingMessage.
    /// </summary>
    public class DeviceClientMessageConverter : IMessageConverter<TelemetryMessage>, IMessageConverter<IncomingMessage>
    {
        // Same Value as IotHub
        static readonly TimeSpan ClockSkewAdjustment = TimeSpan.FromSeconds(30);

        public TelemetryMessage FromMessage(IMessage inputMessage)
        {
            Preconditions.CheckNotNull(inputMessage, nameof(inputMessage));
            Preconditions.CheckArgument(inputMessage.Body != null, "IMessage.Body should not be null");

            var message = new TelemetryMessage(inputMessage.Body);

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

                if (inputMessage.SystemProperties.TryGetNonEmptyValue(SystemProperties.CreationTime, out string creationTime))
                {
                    message.CreationTimeUtc = DateTime.ParseExact(creationTime, "o", CultureInfo.InvariantCulture);
                }

                if (inputMessage.SystemProperties.TryGetNonEmptyValue(SystemProperties.MessageSchema, out string messageSchema))
                {
                    message.MessageSchema = messageSchema;
                }

                if (inputMessage.SystemProperties.TryGetNonEmptyValue(SystemProperties.ComponentName, out string componentName))
                {
                    message.ComponentName = componentName;
                }

                if (inputMessage.SystemProperties.TryGetNonEmptyValue(SystemProperties.InterfaceId, out string interfaceId)
                    && interfaceId.Equals(Constants.SecurityMessageIoTHubInterfaceId, StringComparison.OrdinalIgnoreCase))
                {
                    message.SetAsSecurityMessage();
                }
            }

            return message;
        }

        IMessage IMessageConverter<TelemetryMessage>.ToMessage(TelemetryMessage sourceMessage)
        {
            // TelemetryMessage is outgoing only; this direction is not typically used.
            throw new NotSupportedException("Converting TelemetryMessage to IMessage is not supported. Use IncomingMessage converter instead.");
        }

        public IMessage ToMessage(IncomingMessage sourceMessage)
        {
            EdgeMessage message = new EdgeMessage.Builder(sourceMessage.Payload)
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
            message.SystemProperties.AddIfNonEmpty(SystemProperties.ComponentName, sourceMessage.ComponentName);

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

        IncomingMessage IMessageConverter<IncomingMessage>.FromMessage(IMessage message)
        {
            // IncomingMessage is receive-only; this direction is not typically used.
            throw new NotSupportedException("Converting IMessage to IncomingMessage is not supported. Use TelemetryMessage converter instead.");
        }
    }
}
