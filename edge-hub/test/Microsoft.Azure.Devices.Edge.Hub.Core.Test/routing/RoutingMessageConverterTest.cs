// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test.Routing
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Routing;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core.MessageSources;
    using Moq;
    using Newtonsoft.Json;
    using Xunit;
    using IRoutingMessage = Microsoft.Azure.Devices.Routing.Core.IMessage;
    using RoutingMessage = Microsoft.Azure.Devices.Routing.Core.Message;

    public class RoutingMessageConverterTest
    {
        public static IEnumerable<object[]> GetInvalidHubMessages()
        {
            yield return new object[] { null, typeof(ArgumentNullException) };

            var messageMock = new Mock<IMessage>();
            messageMock.Setup(m => m.Body).Returns((byte[])null);
            yield return new object[] { messageMock.Object, typeof(ArgumentNullException) };
        }

        public static IEnumerable<object[]> GetInvalidRoutingMessages()
        {
            yield return new object[] { null, typeof(ArgumentNullException) };

            var messageMock = new Mock<IRoutingMessage>();
            messageMock.Setup(m => m.Body).Returns((byte[])null);
            yield return new object[] { messageMock.Object, typeof(ArgumentNullException) };
        }

        public static IEnumerable<object[]> GetHubMessagesData()
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

        public static IEnumerable<object[]> GetRoutingMessages()
        {
            var routingMessages = new List<object[]>();

            var data = new
            {
                temperature = 100,
                scale = "Celsius"
            };
            string dataString = JsonConvert.SerializeObject(data);
            byte[] messageBytes = Encoding.UTF8.GetBytes(dataString);
            IDictionary<string, string> properties = new Dictionary<string, string>()
            {
                { "model", "temperature" },
                { "level", "one" }
            };

            IDictionary<string, string> systemProperties = new Dictionary<string, string>()
            {
                { SystemProperties.MessageId, "12345" },
                { SystemProperties.UserId, "UserId10" },
                { SystemProperties.ConnectionDeviceId, "Device10" },
                { SystemProperties.MsgCorrelationId, "CorrId10" },
                { "InvalidSystemProperty", "SomeValue" }
            };
            var routingMessage1 = new RoutingMessage(TelemetryMessageSource.Instance, messageBytes, properties, systemProperties);
            routingMessages.Add(new object[] { routingMessage1 });

            var routingMessage2 = new RoutingMessage(
                TwinChangeEventMessageSource.Instance,
                messageBytes,
                properties,
                systemProperties,
                100,
                DateTime.Today.Subtract(TimeSpan.FromDays(1)),
                DateTime.Today);
            routingMessages.Add(new object[] { routingMessage2 });

            var emptyProperties = new Dictionary<string, string>();
            var emptySystemProperties = new Dictionary<string, string>();
            var routingMessage3 = new RoutingMessage(TelemetryMessageSource.Instance, messageBytes, emptyProperties, emptySystemProperties);
            routingMessages.Add(new object[] { routingMessage3 });

            return routingMessages;
        }

        public static IEnumerable<object[]> GetMessageSourceSystemProperties()
        {
            var theoryData = new List<object[]>();

            theoryData.Add(
                new object[]
                {
                    new Dictionary<string, string>
                    {
                        [SystemProperties.MessageType] = Constants.TwinChangeNotificationMessageType
                    },
                    TwinChangeEventMessageSource.Instance
                });

            theoryData.Add(
                new object[]
                {
                    new Dictionary<string, string>
                    {
                        [SystemProperties.ConnectionModuleId] = "module1",
                        [SystemProperties.OutputName] = "output1"
                    },
                    ModuleMessageSource.Create("module1", "output1")
                });

            theoryData.Add(
                new object[]
                {
                    new Dictionary<string, string>
                    {
                        [SystemProperties.ConnectionModuleId] = "module1",
                        [SystemProperties.OutputName] = "output1"
                    },
                    ModuleMessageSource.Create("module1", "output1")
                });

            theoryData.Add(
                new object[]
                {
                    new Dictionary<string, string>
                    {
                        [SystemProperties.ConnectionModuleId] = "module1",
                    },
                    ModuleMessageSource.Create("module1")
                });

            theoryData.Add(
                new object[]
                {
                    new Dictionary<string, string>(),
                    TelemetryMessageSource.Instance
                });

            return theoryData;
        }

        [Theory]
        [Unit]
        [MemberData(nameof(GetInvalidHubMessages))]
        public void TestFromMessageErrorCases(IMessage inputMessage, Type exceptionType)
        {
            IMessageConverter<IRoutingMessage> messageConverter = new RoutingMessageConverter();
            Assert.Throws(exceptionType, () => messageConverter.FromMessage(inputMessage));
        }

        [Theory]
        [Unit]
        [MemberData(nameof(GetInvalidRoutingMessages))]
        public void TestToMessageErrorCases(IRoutingMessage inputMessage, Type exceptionType)
        {
            IMessageConverter<IRoutingMessage> messageConverter = new RoutingMessageConverter();
            Assert.Throws(exceptionType, () => messageConverter.ToMessage(inputMessage));
        }

        [Theory]
        [Unit]
        [MemberData(nameof(GetHubMessagesData))]
        public void TestFromMessageCases(
            byte[] messageBytes,
            IDictionary<string, string> properties,
            IDictionary<string, string> systemProperties)
        {
            IMessageConverter<IRoutingMessage> messageConverter = new RoutingMessageConverter();
            IMessage inputMessage = new EdgeMessage(messageBytes, properties, systemProperties);
            IRoutingMessage routingMessage = messageConverter.FromMessage(inputMessage);

            Assert.Equal(inputMessage.Body, routingMessage.Body);
            foreach (KeyValuePair<string, string> property in properties)
            {
                Assert.True(routingMessage.Properties.ContainsKey(property.Key));
                Assert.Equal(property.Value, routingMessage.Properties[property.Key]);
            }

            foreach (KeyValuePair<string, string> property in systemProperties)
            {
                Assert.True(routingMessage.SystemProperties.ContainsKey(property.Key));
                Assert.Equal(property.Value, routingMessage.SystemProperties[property.Key]);
            }

            Assert.Equal(DateTime.MinValue, routingMessage.EnqueuedTime);
            Assert.Equal(0, routingMessage.Offset);
            Assert.True(routingMessage.MessageSource.IsTelemetry());
        }

        [Theory]
        [Unit]
        [MemberData(nameof(GetRoutingMessages))]
        public void TestToMessageCases(IRoutingMessage routingMessage)
        {
            IMessageConverter<IRoutingMessage> messageConverter = new RoutingMessageConverter();
            IMessage message = messageConverter.ToMessage(routingMessage);

            Assert.Equal(routingMessage.Body, message.Body);
            Assert.Equal(routingMessage.Properties.Count, message.Properties.Count);
            foreach (KeyValuePair<string, string> property in routingMessage.Properties)
            {
                Assert.True(message.Properties.ContainsKey(property.Key));
                Assert.Equal(property.Value, message.Properties[property.Key]);
            }

            Assert.Equal(routingMessage.SystemProperties.Count, message.SystemProperties.Count);
            foreach (KeyValuePair<string, string> property in routingMessage.SystemProperties)
            {
                Assert.True(message.SystemProperties.ContainsKey(property.Key));
                Assert.Equal(property.Value, message.SystemProperties[property.Key]);
            }
        }

        [Theory]
        [Unit]
        [MemberData(nameof(GetMessageSourceSystemProperties))]
        public void TestGetMessageSource(IDictionary<string, string> systemProperties, BaseMessageSource expectedMessageSource)
        {
            var routingMessageConverter = new RoutingMessageConverter();
            IMessageSource messageSource = routingMessageConverter.GetMessageSource(systemProperties);
            Assert.NotNull(messageSource);
            Assert.IsType(expectedMessageSource.GetType(), messageSource);
            var baseMessageSource = messageSource as BaseMessageSource;
            Assert.True(expectedMessageSource.Equals(baseMessageSource));
        }
    }
}
