// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using Microsoft.Azure.Amqp;
    using Microsoft.Azure.Amqp.Encoding;

    public static class AmqpMessageUtils
    {
        public const string SystemPropertyDiagId = "Diagnostic-Id";

        public const string SystemPropertyDiagnosticCorrelationContext = "Correlation-Context";

        // All DateTimes in the system are encoded using the format "yyyy-MM-ddTHH:mm:ss.fff"
        // when represented as strings. This encoding has a constant length.
        static readonly long Iso8601Length = 24L;

        public static long GetMessageSize(AmqpMessage message, bool includeAllMessageAnnotations = false)
        {
            long size = 0L;
            if (message != null)
            {
                size += GetMessagePropertiesSize(message);
                size += GetMessageApplicationPropertiesSize(message);
                size += GetMessageBodySize(message);
                size += GetMessageAnnotationsSize(message, includeAllMessageAnnotations);
            }

            return size;
        }

        public static byte[] GetPayloadBytes(this AmqpMessage message)
        {
            using (var ms = new MemoryStream())
            {
                message.BodyStream.CopyTo(ms);
                return ms.ToArray();
            }
        }

        static long GetMessagePropertiesSize(AmqpMessage message)
        {
            long size = 0L;
            if (message.Properties != null)
            {
                size += message.Properties.MessageId?.ToString().Length ?? 0L;
                size += message.Properties.CorrelationId?.ToString().Length ?? 0L;

                if (message.Properties.UserId.Array != null)
                {
                    ArraySegment<byte> userid = message.Properties.UserId;
                    size += Encoding.UTF8.GetString(userid.Array, userid.Offset, userid.Count).Length;
                }

                size += message.Properties.To?.ToString().Length ?? 0L;
                size += message.Properties.ContentType.Value?.Length ?? 0L;
                size += message.Properties.ContentEncoding.Value?.Length ?? 0L;
                size += message.Properties.AbsoluteExpiryTime.HasValue ? Iso8601Length : 0L;
            }

            return size;
        }

        static long GetMessageApplicationPropertiesSize(AmqpMessage message)
        {
            long size = 0L;
            if (message.ApplicationProperties?.Map != null)
            {
                foreach (KeyValuePair<MapKey, object> pair in message.ApplicationProperties.Map)
                {
                    size += pair.Key.ToString().Length;
                    size += pair.Value?.ToString().Length ?? 0L;
                }
            }

            return size;
        }

        static long GetMessageAnnotationsSize(AmqpMessage message, bool includeAllMessageAnnotations)
        {
            long size = 0L;
            if (message.MessageAnnotations?.Map != null)
            {
                foreach (KeyValuePair<MapKey, object> pair in message.MessageAnnotations.Map)
                {
                    if (includeAllMessageAnnotations ||
                        pair.Key.Key.Equals(SystemPropertyDiagId) ||
                        pair.Key.Key.Equals(SystemPropertyDiagnosticCorrelationContext))
                    {
                        size += pair.Key.ToString().Length;
                        size += pair.Value?.ToString().Length ?? 0L;
                    }
                }
            }

            return size;
        }

        static long GetMessageBodySize(AmqpMessage message)
        {
            long size = 0L;

            if (SectionExists(message, SectionFlag.Data))
            {
                size += message.BodyStream?.Length ?? 0L;
            }

            return size;
        }

        static bool SectionExists(AmqpMessage message, SectionFlag section)
        {
            return (message.Sections & section) != 0;
        }
    }
}
