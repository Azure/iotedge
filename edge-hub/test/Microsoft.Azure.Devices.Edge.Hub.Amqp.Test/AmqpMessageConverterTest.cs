// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp.Test
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using Microsoft.Azure.Amqp;
    using Microsoft.Azure.Amqp.Framing;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;
    using Constants = Microsoft.Azure.Devices.Edge.Hub.Amqp.Constants;

    [Unit]
    public class AmqpMessageConverterTest
    {
        [Fact]
        public void ToMessageTest()
        {
            // Arrange
            IMessage receivedMessage;
            byte[] bytes = { 1, 2, 3, 4 };
            string messageId = Guid.NewGuid().ToString();
            string correlationId = Guid.NewGuid().ToString();
            using (AmqpMessage amqpMessage = AmqpMessage.Create(new Data { Value = new ArraySegment<byte>(bytes) }))
            {
                amqpMessage.Properties.MessageId = messageId;
                amqpMessage.Properties.CorrelationId = correlationId;
                amqpMessage.Properties.ContentType = "application/json";
                amqpMessage.Properties.ContentEncoding = "UTF-8";

                amqpMessage.ApplicationProperties.Map["Prop1"] = "Value1";
                amqpMessage.ApplicationProperties.Map["Prop2"] = "Value2";

                var messageConverter = new AmqpMessageConverter();

                // Act
                receivedMessage = messageConverter.ToMessage(amqpMessage);
            }

            // Assert
            Assert.NotNull(receivedMessage);
            Assert.Equal(receivedMessage.Body, bytes);
            Assert.Equal(4, receivedMessage.SystemProperties.Count);
            Assert.Equal(2, receivedMessage.Properties.Count);
            Assert.Equal(receivedMessage.SystemProperties[SystemProperties.MessageId], messageId);
            Assert.Equal(receivedMessage.SystemProperties[SystemProperties.MsgCorrelationId], correlationId);
            Assert.Equal("application/json", receivedMessage.SystemProperties[SystemProperties.ContentType]);
            Assert.Equal("UTF-8", receivedMessage.SystemProperties[SystemProperties.ContentEncoding]);
            Assert.Equal("Value1", receivedMessage.Properties["Prop1"]);
            Assert.Equal("Value2", receivedMessage.Properties["Prop2"]);
        }

        [Fact]
        public void ToMessageNoAppPropsTest()
        {
            // Arrange
            IMessage receivedMessage;
            byte[] bytes = { 1, 2, 3, 4 };
            string messageId = Guid.NewGuid().ToString();
            string correlationId = Guid.NewGuid().ToString();
            using (AmqpMessage amqpMessage = AmqpMessage.Create(new Data { Value = new ArraySegment<byte>(bytes) }))
            {
                amqpMessage.Properties.MessageId = messageId;
                amqpMessage.Properties.CorrelationId = correlationId;
                amqpMessage.Properties.ContentType = "application/json";
                amqpMessage.Properties.ContentEncoding = "UTF-8";

                amqpMessage.ApplicationProperties = null;

                var messageConverter = new AmqpMessageConverter();

                // Act
                receivedMessage = messageConverter.ToMessage(amqpMessage);
            }

            // Assert
            Assert.NotNull(receivedMessage);
            Assert.Equal(receivedMessage.Body, bytes);
            Assert.Equal(4, receivedMessage.SystemProperties.Count);
            Assert.Equal(0, receivedMessage.Properties.Count);
            Assert.Equal(receivedMessage.SystemProperties[SystemProperties.MessageId], messageId);
            Assert.Equal(receivedMessage.SystemProperties[SystemProperties.MsgCorrelationId], correlationId);
            Assert.Equal("application/json", receivedMessage.SystemProperties[SystemProperties.ContentType]);
            Assert.Equal("UTF-8", receivedMessage.SystemProperties[SystemProperties.ContentEncoding]);
        }

        [Fact]
        public void ToMessageTest_AllProperties()
        {
            // Arrange
            IMessage receivedMessage;
            byte[] bytes = { 1, 2, 3, 4 };
            string messageId = Guid.NewGuid().ToString();
            string correlationId = Guid.NewGuid().ToString();
            string contentType = "application/json";
            string contentEncoding = "UTF-8";
            string to = "d1";
            string userId = "userId1";
            var expiryTime = new DateTime(2018, 2, 3, 02, 03, 04, DateTimeKind.Utc);
            string creationTime = new DateTime(2018, 1, 1, 02, 03, 04, DateTimeKind.Utc).ToString("o");
            var enqueuedTime = new DateTime(2018, 4, 5, 04, 05, 06, DateTimeKind.Utc);
            byte deliveryCount = 10;
            string lockToken = Guid.NewGuid().ToString();
            ulong sequenceNumber = 100;
            string messageSchema = "testSchema";
            string operation = "foo";
            string outputName = "output1";

            using (AmqpMessage amqpMessage = AmqpMessage.Create(new Data { Value = new ArraySegment<byte>(bytes) }))
            {
                amqpMessage.Properties.MessageId = messageId;
                amqpMessage.Properties.CorrelationId = correlationId;
                amqpMessage.Properties.ContentType = contentType;
                amqpMessage.Properties.ContentEncoding = contentEncoding;
                amqpMessage.Properties.To = to;
                amqpMessage.Properties.UserId = new ArraySegment<byte>(Encoding.UTF8.GetBytes(userId));
                amqpMessage.Properties.AbsoluteExpiryTime = expiryTime;

                amqpMessage.MessageAnnotations.Map[Constants.MessageAnnotationsEnqueuedTimeKey] = enqueuedTime;
                amqpMessage.MessageAnnotations.Map[Constants.MessageAnnotationsDeliveryCountKey] = deliveryCount;
                amqpMessage.MessageAnnotations.Map[Constants.MessageAnnotationsLockTokenName] = lockToken;
                amqpMessage.MessageAnnotations.Map[Constants.MessageAnnotationsSequenceNumberName] = sequenceNumber;

                amqpMessage.ApplicationProperties.Map[Constants.MessagePropertiesMessageSchemaKey] = messageSchema;
                amqpMessage.ApplicationProperties.Map[Constants.MessagePropertiesCreationTimeKey] = creationTime;
                amqpMessage.ApplicationProperties.Map[Constants.MessagePropertiesOperationKey] = operation;
                amqpMessage.ApplicationProperties.Map[Constants.MessagePropertiesOutputNameKey] = outputName;

                amqpMessage.ApplicationProperties.Map["Prop1"] = "Value1";
                amqpMessage.ApplicationProperties.Map["Prop2"] = "Value2";

                var messageConverter = new AmqpMessageConverter();

                // Act
                receivedMessage = messageConverter.ToMessage(amqpMessage);
            }

            // Assert
            Assert.NotNull(receivedMessage);
            Assert.Equal(receivedMessage.Body, bytes);
            Assert.Equal(15, receivedMessage.SystemProperties.Count);
            Assert.Equal(2, receivedMessage.Properties.Count);

            Assert.Equal(receivedMessage.SystemProperties[SystemProperties.MessageId], messageId);
            Assert.Equal(receivedMessage.SystemProperties[SystemProperties.MsgCorrelationId], correlationId);
            Assert.Equal(receivedMessage.SystemProperties[SystemProperties.ContentType], contentType);
            Assert.Equal(receivedMessage.SystemProperties[SystemProperties.ContentEncoding], contentEncoding);
            Assert.Equal(receivedMessage.SystemProperties[SystemProperties.To], to);
            Assert.Equal(receivedMessage.SystemProperties[SystemProperties.UserId], userId);
            Assert.Equal(receivedMessage.SystemProperties[SystemProperties.ExpiryTimeUtc], expiryTime.ToString("o"));
            Assert.Equal(receivedMessage.SystemProperties[SystemProperties.EnqueuedTime], enqueuedTime.ToString("o"));
            Assert.Equal(receivedMessage.SystemProperties[SystemProperties.DeliveryCount], deliveryCount.ToString());
            Assert.Equal(receivedMessage.SystemProperties[SystemProperties.LockToken], lockToken);
            Assert.Equal(receivedMessage.SystemProperties[SystemProperties.SequenceNumber], sequenceNumber.ToString());
            Assert.Equal(receivedMessage.SystemProperties[SystemProperties.MessageSchema], messageSchema);
            Assert.Equal(receivedMessage.SystemProperties[SystemProperties.CreationTime], creationTime);
            Assert.Equal(receivedMessage.SystemProperties[SystemProperties.Operation], operation);
            Assert.Equal(receivedMessage.SystemProperties[SystemProperties.OutputName], outputName);

            Assert.Equal("Value1", receivedMessage.Properties["Prop1"]);
            Assert.Equal("Value2", receivedMessage.Properties["Prop2"]);
        }

        [Fact]
        public void FromMessageTest()
        {
            // Arrange
            byte[] bytes = { 1, 2, 3, 4 };
            string messageId = Guid.NewGuid().ToString();
            string correlationId = Guid.NewGuid().ToString();
            string contentType = "UTF-8";
            string contentEncoding = "application/json";

            var systemProperties = new Dictionary<string, string>
            {
                [SystemProperties.MessageId] = messageId,
                [SystemProperties.MsgCorrelationId] = correlationId,
                [SystemProperties.ContentType] = contentType,
                [SystemProperties.ContentEncoding] = contentEncoding,
            };

            var properties = new Dictionary<string, string>
            {
                ["Prop1"] = "Value1",
                ["Prop2"] = "Value2"
            };

            byte[] GetMessageBody(AmqpMessage sourceMessage)
            {
                using (var ms = new MemoryStream())
                {
                    sourceMessage.BodyStream.CopyTo(ms);
                    return ms.ToArray();
                }
            }

            var message = new EdgeMessage(bytes, properties, systemProperties);
            var messageConverter = new AmqpMessageConverter();

            // Act
            using (AmqpMessage amqpMessage = messageConverter.FromMessage(message))
            {
                // Assert
                Assert.NotNull(amqpMessage);
                Assert.Equal(bytes, GetMessageBody(amqpMessage));
                Assert.Equal(messageId, amqpMessage.Properties.MessageId.ToString());
                Assert.Equal(correlationId, amqpMessage.Properties.CorrelationId.ToString());
                Assert.Equal(contentEncoding, amqpMessage.Properties.ContentEncoding.ToString());
                Assert.Equal(contentType, amqpMessage.Properties.ContentType.ToString());
                Assert.Equal("Value1", amqpMessage.ApplicationProperties.Map["Prop1"].ToString());
                Assert.Equal("Value2", amqpMessage.ApplicationProperties.Map["Prop2"].ToString());
            }
        }

        [Fact]
        public void FromMessageTest_AllProperties()
        {
            // Arrange
            byte[] bytes = { 1, 2, 3, 4 };
            string messageId = Guid.NewGuid().ToString();
            string correlationId = Guid.NewGuid().ToString();
            string contentType = "UTF-8";
            string contentEncoding = "application/json";
            string to = "d1";
            string userId = "userId1";
            var expiryTime = new DateTime(2018, 2, 3, 02, 03, 04, DateTimeKind.Utc);
            string creationTime = new DateTime(2018, 1, 1, 02, 03, 04, DateTimeKind.Utc).ToString("o");
            var enqueuedTime = new DateTime(2018, 4, 5, 04, 05, 06, DateTimeKind.Utc);
            byte deliveryCount = 10;
            string lockToken = Guid.NewGuid().ToString();
            ulong sequenceNumber = 100;
            string messageSchema = "testSchema";
            string operation = "foo";
            string inputName = "inputName";
            string outputName = "outputName";
            string connectionDeviceId = "edgeDevice1";
            string connectionModuleId = "module1";

            var systemProperties = new Dictionary<string, string>
            {
                [SystemProperties.MessageId] = messageId,
                [SystemProperties.MsgCorrelationId] = correlationId,
                [SystemProperties.ContentType] = contentType,
                [SystemProperties.ContentEncoding] = contentEncoding,
                [SystemProperties.To] = to,
                [SystemProperties.UserId] = userId,
                [SystemProperties.ExpiryTimeUtc] = expiryTime.ToString("o"),
                [SystemProperties.EnqueuedTime] = enqueuedTime.ToString("o"),
                [SystemProperties.DeliveryCount] = deliveryCount.ToString(),
                [SystemProperties.LockToken] = lockToken,
                [SystemProperties.SequenceNumber] = sequenceNumber.ToString(),
                [SystemProperties.MessageSchema] = messageSchema,
                [SystemProperties.CreationTime] = creationTime,
                [SystemProperties.Operation] = operation,
                [SystemProperties.InputName] = inputName,
                [SystemProperties.OutputName] = outputName,
                [SystemProperties.ConnectionDeviceId] = connectionDeviceId,
                [SystemProperties.ConnectionModuleId] = connectionModuleId
            };

            var properties = new Dictionary<string, string>
            {
                ["Prop1"] = "Value1",
                ["Prop2"] = "Value2"
            };

            byte[] GetMessageBody(AmqpMessage sourceMessage)
            {
                using (var ms = new MemoryStream())
                {
                    sourceMessage.BodyStream.CopyTo(ms);
                    return ms.ToArray();
                }
            }

            var message = new EdgeMessage(bytes, properties, systemProperties);
            var messageConverter = new AmqpMessageConverter();

            // Act
            using (AmqpMessage amqpMessage = messageConverter.FromMessage(message))
            {
                // Assert
                Assert.NotNull(amqpMessage);
                Assert.Equal(bytes, GetMessageBody(amqpMessage));

                Assert.Equal(messageId, amqpMessage.Properties.MessageId.ToString());
                Assert.Equal(correlationId, amqpMessage.Properties.CorrelationId.ToString());
                Assert.Equal(contentEncoding, amqpMessage.Properties.ContentEncoding.ToString());
                Assert.Equal(contentType, amqpMessage.Properties.ContentType.ToString());
                Assert.Equal(to, amqpMessage.Properties.To.ToString());
                Assert.Equal(userId, Encoding.UTF8.GetString(amqpMessage.Properties.UserId.Array));
                Assert.Equal(expiryTime, amqpMessage.Properties.AbsoluteExpiryTime.HasValue ? amqpMessage.Properties.AbsoluteExpiryTime.Value : DateTime.MinValue);

                Assert.Equal(enqueuedTime, amqpMessage.MessageAnnotations.Map[Constants.MessageAnnotationsEnqueuedTimeKey]);
                Assert.Equal(deliveryCount, amqpMessage.MessageAnnotations.Map[Constants.MessageAnnotationsDeliveryCountKey]);
                Assert.Equal(lockToken, amqpMessage.MessageAnnotations.Map[Constants.MessageAnnotationsLockTokenName]);
                Assert.Equal(sequenceNumber, amqpMessage.MessageAnnotations.Map[Constants.MessageAnnotationsSequenceNumberName]);
                Assert.Equal(inputName, amqpMessage.MessageAnnotations.Map[Constants.MessageAnnotationsInputNameKey]);
                Assert.Equal(connectionDeviceId, amqpMessage.MessageAnnotations.Map[Constants.MessageAnnotationsConnectionDeviceId]);
                Assert.Equal(connectionModuleId, amqpMessage.MessageAnnotations.Map[Constants.MessageAnnotationsConnectionModuleId]);

                Assert.Equal(messageSchema, amqpMessage.ApplicationProperties.Map[Constants.MessagePropertiesMessageSchemaKey]);
                Assert.Equal(creationTime, amqpMessage.ApplicationProperties.Map[Constants.MessagePropertiesCreationTimeKey]);
                Assert.Equal(operation, amqpMessage.ApplicationProperties.Map[Constants.MessagePropertiesOperationKey]);
                Assert.False(amqpMessage.ApplicationProperties.Map.TryGetValue(Constants.MessagePropertiesOutputNameKey, out string _));

                Assert.Equal("Value1", amqpMessage.ApplicationProperties.Map["Prop1"].ToString());
                Assert.Equal("Value2", amqpMessage.ApplicationProperties.Map["Prop2"].ToString());
            }
        }
    }
}
