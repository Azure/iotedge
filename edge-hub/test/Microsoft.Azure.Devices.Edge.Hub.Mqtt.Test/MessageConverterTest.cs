
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt.Test
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Newtonsoft.Json;
    using Xunit;

    public class MessageConverterTest
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
                { SystemProperties.DeviceId, "Device10" },
                { SystemProperties.CorrelationId, "CorrId10" },
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
            IMessageConverter<Message> messageConverter = new MqttMessageConverter();
            Assert.Throws(exceptionType, () => messageConverter.FromMessage(inputMessage));
        }

        [Theory]
        [Unit]
        [MemberData(nameof(GetValidMessagesData))]
        public void TestValidCases(byte[] messageBytes,
            IDictionary<string, string> properties,
            IDictionary<string, string> systemProperties)
        {
            IMessageConverter<Message> messageConverter = new MqttMessageConverter();
            IMessage inputMessage = new Core.Test.Message(messageBytes, properties, systemProperties);
            Message proxyMessage = messageConverter.FromMessage(inputMessage);

            Assert.Equal(inputMessage.Body, proxyMessage.GetBytes());
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
                systemProperties.ContainsKey(SystemProperties.CorrelationId) ? systemProperties[SystemProperties.CorrelationId] : null,
                proxyMessage.CorrelationId);

            Assert.Equal(DateTime.MinValue, proxyMessage.ExpiryTimeUtc);
            Assert.Equal(DateTime.MinValue, proxyMessage.EnqueuedTimeUtc);
            Assert.True(proxyMessage.SequenceNumber == 0);
            Assert.Null(proxyMessage.To);
        }

        [Theory]
        [Unit]
        [MemberData(nameof(GetValidMessagesData))]
        public void TestValidCasesToMessage(byte[] messageBytes,
            IDictionary<string, string> properties,
            IDictionary<string, string> systemProperties)
        {
            IMessageConverter<Message> messageConverter = new MqttMessageConverter();
            var inputMessage = new Message(messageBytes);
            IMessage mqttMessage = messageConverter.ToMessage(inputMessage);

            Assert.Equal(inputMessage.GetBytes(), mqttMessage.Body);
        }
    }
}
