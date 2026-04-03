// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy.Test
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Newtonsoft.Json;
    using Xunit;

    public class DeviceClientMessageConverterTest
    {
        public static IEnumerable<object[]> GetInvalidMessages()
        {
            yield return new object[] { null, typeof(ArgumentNullException) };

            var messageMock = new Mock<IMessage>();
            messageMock.Setup(m => m.Body).Returns((byte[])null);
            yield return new object[] { messageMock.Object, typeof(ArgumentException) };
        }

        public static IEnumerable<object[]> GetValidMessagesData()
        {
            var data = new
            {
                temperature = 100,
                scale = "Celsius"
            };
            string dataString = JsonConvert.SerializeObject(data);
            byte[] messageBytes = Encoding.UTF8.GetBytes(dataString);
            var properties = new Dictionary<string, string>()
            {
                { "model", "temperature" },
                { "level", "one" }
            };
            var systemProperties = new Dictionary<string, string>()
            {
                { SystemProperties.MessageId, "12345" },
                { SystemProperties.UserId, "UserId10" },
                { SystemProperties.ConnectionDeviceId, "Device10" },
                { SystemProperties.MsgCorrelationId, "CorrId10" },
                { "InvalidSystemProperty", "SomeValue" }
            };
            yield return new object[] { messageBytes, properties, systemProperties };

            var emptyProperties = new Dictionary<string, string>();
            var emptySystemProperties = new Dictionary<string, string>();
            yield return new object[] { messageBytes, emptyProperties, emptySystemProperties };
        }

        [Theory]
        [Unit]
        [MemberData(nameof(GetInvalidMessages))]
        public void TestErrorCases(IMessage inputMessage, Type exceptionType)
        {
            IMessageConverter<TelemetryMessage> messageConverter = new DeviceClientMessageConverter();
            Assert.Throws(exceptionType, () => messageConverter.FromMessage(inputMessage));
        }

        [Theory]
        [Unit]
        [MemberData(nameof(GetValidMessagesData))]
        public void TestValidCases(
            byte[] messageBytes,
            IDictionary<string, string> properties,
            IDictionary<string, string> systemProperties)
        {
            IMessageConverter<TelemetryMessage> messageConverter = new DeviceClientMessageConverter();
            IMessage inputMessage = new EdgeMessage(messageBytes, properties, systemProperties);
            TelemetryMessage proxyMessage = messageConverter.FromMessage(inputMessage);

            Assert.Equal(inputMessage.Body, proxyMessage.Payload);
            foreach (KeyValuePair<string, string> property in properties)
            {
                Assert.True(proxyMessage.Properties.ContainsKey(property.Key));
                Assert.Equal(property.Value, proxyMessage.Properties[property.Key]);
            }

            Assert.Equal(
                systemProperties.ContainsKey(SystemProperties.MessageId) ? systemProperties[SystemProperties.MessageId] : null,
                proxyMessage.MessageId);

            Assert.Equal(
                systemProperties.ContainsKey(SystemProperties.UserId) ? systemProperties[SystemProperties.UserId] : null,
                proxyMessage.UserId);

            Assert.Equal(
                systemProperties.ContainsKey(SystemProperties.MsgCorrelationId) ? systemProperties[SystemProperties.MsgCorrelationId] : null,
                proxyMessage.CorrelationId);
        }

        [Theory]
        [Unit]
        [MemberData(nameof(GetValidMessagesData))]
        public void TestValidCasesToMessage(
            byte[] messageBytes,
            IDictionary<string, string> properties,
            IDictionary<string, string> systemProperties)
        {
            IMessageConverter<IncomingMessage> messageConverter = new DeviceClientMessageConverter();
            var inputMessage = new IncomingMessage(messageBytes);
            IMessage mqttMessage = messageConverter.ToMessage(inputMessage);

            Assert.Equal(messageBytes, mqttMessage.Body);
        }

        [Unit]
        [Fact]
        public void TestFromMessage_AllSystemProperties()
        {
            string creationTime = new DateTime(2018, 01, 01).ToString("o");
            var properties = new Dictionary<string, string>
            {
                ["Foo"] = "Bar",
                ["Prop2"] = "Value2"
            };

            var systemProperties = new Dictionary<string, string>
            {
                [SystemProperties.ContentEncoding] = "utf-8",
                [SystemProperties.ContentType] = "application/json",
                [SystemProperties.MessageSchema] = "schema1",
                [SystemProperties.To] = "foo",
                [SystemProperties.UserId] = "user1",
                [SystemProperties.MsgCorrelationId] = "1234",
                [SystemProperties.MessageId] = "m1",
                [SystemProperties.CreationTime] = creationTime,
                [SystemProperties.InterfaceId] = Constants.SecurityMessageIoTHubInterfaceId,
                [SystemProperties.ComponentName] = "Test Component"
            };

            var message = Mock.Of<IMessage>(
                m =>
                    m.Body == new byte[] { 1, 2, 3 } &&
                    m.Properties == properties &&
                    m.SystemProperties == systemProperties);

            IMessageConverter<TelemetryMessage> messageConverter = new DeviceClientMessageConverter();
            TelemetryMessage clientMessage = messageConverter.FromMessage(message);

            Assert.NotNull(clientMessage);
            Assert.Equal(clientMessage.Payload, message.Body);
            Assert.Equal("Bar", clientMessage.Properties["Foo"]);
            Assert.Equal("Value2", clientMessage.Properties["Prop2"]);

            Assert.Equal("utf-8", clientMessage.ContentEncoding);
            Assert.Equal("application/json", clientMessage.ContentType);
            Assert.Equal("schema1", clientMessage.MessageSchema);
            Assert.Equal("1234", clientMessage.CorrelationId);
            Assert.Equal("m1", clientMessage.MessageId);
            Assert.Equal(creationTime, clientMessage.CreationTimeUtc.ToString("o"));
            Assert.True(clientMessage.IsSecurityMessage);
            Assert.Equal("Test Component", clientMessage.ComponentName);
        }

        [Unit]
        [Fact]
        public void TestToMessage_AllSystemProperties()
        {
            var creationTime = new DateTime(2018, 01, 01, 0, 0, 0, DateTimeKind.Utc);

            var body = new byte[] { 1, 2, 3 };
            var clientMessage = new IncomingMessage(body);
            clientMessage.Properties["Foo"] = "Bar";
            clientMessage.Properties["Prop2"] = "Value2";

            clientMessage.ContentEncoding = "utf-8";
            clientMessage.ContentType = "application/json";
            clientMessage.MessageSchema = "schema1";
            clientMessage.To = "foo";
            clientMessage.UserId = "user1";
            clientMessage.CorrelationId = "1234";
            clientMessage.MessageId = "m1";
            clientMessage.CreationTimeUtc = creationTime;
            clientMessage.ComponentName = "Test Component";

            IMessageConverter<IncomingMessage> messageConverter = new DeviceClientMessageConverter();
            IMessage message = messageConverter.ToMessage(clientMessage);

            Assert.NotNull(message);
            Assert.Equal(body, message.Body);
            Assert.Equal("Bar", message.Properties["Foo"]);
            Assert.Equal("Value2", message.Properties["Prop2"]);

            Assert.Equal(11, message.SystemProperties.Count);
            Assert.Equal("utf-8", message.SystemProperties[SystemProperties.ContentEncoding]);
            Assert.Equal("application/json", message.SystemProperties[SystemProperties.ContentType]);
            Assert.Equal("schema1", message.SystemProperties[SystemProperties.MessageSchema]);
            Assert.Equal("foo", message.SystemProperties[SystemProperties.To]);
            Assert.Equal("1234", message.SystemProperties[SystemProperties.MsgCorrelationId]);
            Assert.Equal("m1", message.SystemProperties[SystemProperties.MessageId]);
            Assert.Equal(creationTime.ToString("o"), message.SystemProperties[SystemProperties.CreationTime]);
            Assert.Equal(DateTime.Parse(message.SystemProperties[SystemProperties.CreationTime], null, DateTimeStyles.RoundtripKind), creationTime);
            Assert.Equal("0", message.SystemProperties[SystemProperties.DeliveryCount]);
            Assert.Equal("Test Component", message.SystemProperties[SystemProperties.ComponentName]);
        }
    }
}
