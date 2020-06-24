// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt.Test
{
    using System;
    using System.Collections.Generic;
    using DotNetty.Buffers;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Unit]
    public class MessageAddressConverterTest
    {
        static readonly string[] DontCareInput = { string.Empty };
        static readonly IDictionary<string, string> EmptyProperties = new Dictionary<string, string>();

        static readonly IDictionary<string, string> DontCareOutput = new Dictionary<string, string>
        {
            ["DontCare"] = string.Empty
        };

        static readonly IByteBuffer Payload = new ByteBufferConverter(PooledByteBufferAllocator.Default).ToByteBuffer(new byte[] { 1, 2, 3 });

        [Fact]
        public void TestMessageAddressConverterWithEmptyConversionConfig()
        {
            var emptyConversionConfig = new MessageAddressConversionConfiguration();
            Assert.Throws<ArgumentException>(() => new MessageAddressConverter(emptyConversionConfig));
        }

        [Fact]
        public void TryDeriveAddressWorksWithOneTemplate()
        {
            string address;
            var testTemplate = new Dictionary<string, string>()
            {
                ["Test"] = "a/{b}/c"
            };
            var config = new MessageAddressConversionConfiguration(
                DontCareInput,
                testTemplate);
            Mock<IMessage> message = CreateMessageWithSystemProps(
                new Dictionary<string, string>()
                {
                    ["b"] = "123"
                });

            var converter = new MessageAddressConverter(config);
            bool result = converter.TryBuildProtocolAddressFromEdgeHubMessage("Test", message.Object, EmptyProperties, out address);

            Assert.True(result);
            Assert.NotNull(address);
            Assert.NotEmpty(address);
            Assert.Equal("a/123/c", address);
            message.VerifyAll();
        }

        [Fact]
        public void TryDeriveAddressWorksWithMoreThanOneTemplate()
        {
            string address;
            var testTemplate = new Dictionary<string, string>()
            {
                ["Test1"] = "a/{b}/c",
                ["Test2"] = "d/{e}/f",
                ["Test3"] = "x/{y}/z"
            };
            var config = new MessageAddressConversionConfiguration(
                DontCareInput,
                testTemplate);
            Mock<IMessage> message = CreateMessageWithSystemProps(
                new Dictionary<string, string>()
                {
                    ["e"] = "123"
                });

            var converter = new MessageAddressConverter(config);
            bool result = converter.TryBuildProtocolAddressFromEdgeHubMessage("Test2", message.Object, EmptyProperties, out address);

            Assert.True(result);
            Assert.NotNull(address);
            Assert.NotEmpty(address);
            Assert.Equal("d/123/f", address);
            message.VerifyAll();
        }

        [Fact]
        public void TryDeriveAddressWorksWithMultipleVariableTemplate()
        {
            string address;
            var testTemplate = new Dictionary<string, string>()
            {
                ["Test"] = "a/{b}/c/{d}/e/{f}/",
            };
            var config = new MessageAddressConversionConfiguration(
                DontCareInput,
                testTemplate);
            Mock<IMessage> message = CreateMessageWithSystemProps(
                new Dictionary<string, string>()
                {
                    ["b"] = "123",
                    ["d"] = "456",
                    ["f"] = "789"
                });

            var converter = new MessageAddressConverter(config);
            bool result = converter.TryBuildProtocolAddressFromEdgeHubMessage("Test", message.Object, EmptyProperties, out address);

            Assert.True(result);
            Assert.NotNull(address);
            Assert.NotEmpty(address);
            Assert.Equal("a/123/c/456/e/789/", address);
            message.VerifyAll();
        }

        [Fact]
        public void TryDeriveAddressFailsWithInvalidTemplate()
        {
            string address;
            var testTemplate = new Dictionary<string, string>()
            {
                ["Test"] = "a/{b}/c",
            };
            var config = new MessageAddressConversionConfiguration(
                DontCareInput,
                testTemplate);
            Mock<IMessage> message = CreateMessageWithSystemProps(
                new Dictionary<string, string>()
                {
                    ["b"] = "123"
                });

            var converter = new MessageAddressConverter(config);
            bool result = converter.TryBuildProtocolAddressFromEdgeHubMessage("BadTest", message.Object, EmptyProperties, out address);

            Assert.False(result);
            Assert.Null(address);
        }

        [Fact]
        public void TestTryDeriveAddressFailsWithEmptyProperties()
        {
            string address;
            var testTemplate = new Dictionary<string, string>()
            {
                ["Test"] = "a/{b}/c",
            };
            var config = new MessageAddressConversionConfiguration(
                DontCareInput,
                testTemplate);
            Mock<IMessage> message = CreateMessageWithSystemProps(new Dictionary<string, string>());

            var converter = new MessageAddressConverter(config);
            bool result = converter.TryBuildProtocolAddressFromEdgeHubMessage("Test", message.Object, EmptyProperties, out address);

            Assert.False(result);
            Assert.Null(address);
            message.VerifyAll();
        }

        [Fact]
        public void TestTryDeriveAddressFailsWithNoValidProperties()
        {
            string address;
            var testTemplate = new Dictionary<string, string>()
            {
                ["Test"] = "a/{b}/c",
            };
            var config = new MessageAddressConversionConfiguration(
                DontCareInput,
                testTemplate);
            Mock<IMessage> message = CreateMessageWithSystemProps(
                new Dictionary<string, string>()
                {
                    ["a"] = "123"
                });

            var converter = new MessageAddressConverter(config);
            bool result = converter.TryBuildProtocolAddressFromEdgeHubMessage("Test", message.Object, EmptyProperties, out address);

            Assert.False(result);
            Assert.Null(address);
            message.VerifyAll();
        }

        [Fact]
        public void TestTryDeriveAddressFailsWithPartiallyMissingProperties()
        {
            string address;
            var testTemplate = new Dictionary<string, string>()
            {
                ["Test"] = "a/{b}/c/{d}/e/{f}/",
            };
            var config = new MessageAddressConversionConfiguration(
                DontCareInput,
                testTemplate);
            Mock<IMessage> message = CreateMessageWithSystemProps(
                new Dictionary<string, string>()
                {
                    ["b"] = "123",
                    ["f"] = "789",
                });

            var converter = new MessageAddressConverter(config);
            bool result = converter.TryBuildProtocolAddressFromEdgeHubMessage("Test", message.Object, EmptyProperties, out address);

            Assert.False(result);
            Assert.Null(address);
            message.VerifyAll();
        }

        [Fact]
        public void TryDeriveAddressWorksWithOneTemplateWithParameters()
        {
            string address;
            var testTemplate = new Dictionary<string, string>()
            {
                ["Test"] = "a/{b}/c"
            };
            var config = new MessageAddressConversionConfiguration(
                DontCareInput,
                testTemplate);

            var systemProperties = new Dictionary<string, string>()
            {
                ["b"] = "123",
                [SystemProperties.OutgoingSystemPropertiesMap[SystemProperties.ConnectionDeviceId]] = "Device1",
                [SystemProperties.OutgoingSystemPropertiesMap[SystemProperties.ConnectionModuleId]] = "Module1"
            };

            var properties = new Dictionary<string, string>()
            {
                ["Prop1"] = "Val1",
                ["Prop2"] = "Val2",
                [SystemProperties.OutgoingSystemPropertiesMap[SystemProperties.ConnectionDeviceId]] = "Device1",
                [SystemProperties.OutgoingSystemPropertiesMap[SystemProperties.ConnectionModuleId]] = "Module1"
            };

            Mock<IMessage> message = CreateMessageWithSystemProps(systemProperties);

            var converter = new MessageAddressConverter(config);
            bool result = converter.TryBuildProtocolAddressFromEdgeHubMessage("Test", message.Object, properties, out address);

            Assert.True(result);
            Assert.NotNull(address);
            Assert.NotEmpty(address);
            Assert.Equal("a/123/c/Prop1=Val1&Prop2=Val2&%24.cdid=Device1&%24.cmid=Module1", address);
            message.VerifyAll();
        }

        [Fact]
        public void TryDeriveAddressWorksWithMoreThanOneTemplateWithParameters()
        {
            string address;
            var testTemplate = new Dictionary<string, string>()
            {
                ["Test1"] = "a/{b}/c",
                ["Test2"] = "d/{e}/f",
                ["Test3"] = "x/{y}/z"
            };
            var config = new MessageAddressConversionConfiguration(
                DontCareInput,
                testTemplate);

            var systemProperties = new Dictionary<string, string>()
            {
                ["e"] = "123",
                [SystemProperties.OutgoingSystemPropertiesMap[SystemProperties.ConnectionDeviceId]] = "Device1"
            };

            var properties = new Dictionary<string, string>()
            {
                ["Prop1"] = "Val1",
                ["Prop2"] = "Val2",
                [SystemProperties.OutgoingSystemPropertiesMap[SystemProperties.ConnectionDeviceId]] = "Device1"
            };

            Mock<IMessage> message = CreateMessageWithSystemProps(systemProperties);

            var converter = new MessageAddressConverter(config);
            bool result = converter.TryBuildProtocolAddressFromEdgeHubMessage("Test2", message.Object, properties, out address);

            Assert.True(result);
            Assert.NotNull(address);
            Assert.NotEmpty(address);
            Assert.Equal("d/123/f/Prop1=Val1&Prop2=Val2&%24.cdid=Device1", address);
            message.VerifyAll();
        }

        [Fact]
        public void TryDeriveAddressWorksWithMultipleVariableTemplateWithParameters()
        {
            string address;
            var testTemplate = new Dictionary<string, string>()
            {
                ["Test"] = "a/{b}/c/{d}/e/{f}/",
            };
            var config = new MessageAddressConversionConfiguration(
                DontCareInput,
                testTemplate);

            var systemProperties = new Dictionary<string, string>()
            {
                ["b"] = "123",
                ["d"] = "456",
                ["f"] = "789",
                [SystemProperties.OutgoingSystemPropertiesMap[SystemProperties.ConnectionDeviceId]] = "Device1",
                [SystemProperties.OutgoingSystemPropertiesMap[SystemProperties.ConnectionModuleId]] = "Module1"
            };

            var properties = new Dictionary<string, string>()
            {
                ["Prop1"] = "Val1",
                ["Prop2"] = "Val2",
                [SystemProperties.OutgoingSystemPropertiesMap[SystemProperties.ConnectionDeviceId]] = "Device1",
                [SystemProperties.OutgoingSystemPropertiesMap[SystemProperties.ConnectionModuleId]] = "Module1"
            };

            Mock<IMessage> message = CreateMessageWithSystemProps(systemProperties);

            var converter = new MessageAddressConverter(config);
            bool result = converter.TryBuildProtocolAddressFromEdgeHubMessage("Test", message.Object, properties, out address);

            Assert.True(result);
            Assert.NotNull(address);
            Assert.NotEmpty(address);
            Assert.Equal("a/123/c/456/e/789/Prop1=Val1&Prop2=Val2&%24.cdid=Device1&%24.cmid=Module1", address);
            message.VerifyAll();
        }

        [Fact]
        public void TestTryParseAddressIntoMessageProperties()
        {
            IList<string> input = new List<string>() { "a/{b}/c/{d}/" };
            var config = new MessageAddressConversionConfiguration(
                input,
                DontCareOutput);
            var converter = new MessageAddressConverter(config);

            string address = "a/bee/c/dee/";
            ProtocolGatewayMessage message = new ProtocolGatewayMessage.Builder(Payload, address)
                .Build();
            bool status = converter.TryParseProtocolMessagePropsFromAddress(message);
            Assert.True(status);
            string value;
            Assert.True(message.Properties.TryGetValue("b", out value));
            Assert.Equal("bee", value);
            Assert.True(message.Properties.TryGetValue("d", out value));
            Assert.Equal("dee", value);
        }

        [Fact]
        public void TestTryParseAddressIntoMessagePropertiesMultipleInput()
        {
            IList<string> input = new List<string>() { "a/{b}/c/{d}/", "e/{f}/g/{h}/" };
            var config = new MessageAddressConversionConfiguration(
                input,
                DontCareOutput);
            var converter = new MessageAddressConverter(config);

            string address = "a/bee/c/dee/";
            ProtocolGatewayMessage message = new ProtocolGatewayMessage.Builder(Payload, address)
                .Build();
            bool status = converter.TryParseProtocolMessagePropsFromAddress(message);
            Assert.True(status);
            string value;
            Assert.True(message.Properties.TryGetValue("b", out value));
            Assert.Equal("bee", value);
            Assert.True(message.Properties.TryGetValue("d", out value));
            Assert.Equal("dee", value);
            Assert.Equal(2, message.Properties.Count);
        }

        [Fact]
        public void TestTryParseAddressIntoMessagePropertiesFailsNoMatch()
        {
            IList<string> input = new List<string>() { "a/{b}/c/{d}/" };
            var config = new MessageAddressConversionConfiguration(
                input,
                DontCareOutput);
            var converter = new MessageAddressConverter(config);

            string address = "a/bee/c/";
            ProtocolGatewayMessage message = new ProtocolGatewayMessage.Builder(Payload, address)
                .Build();
            bool status = converter.TryParseProtocolMessagePropsFromAddress(message);
            Assert.False(status);
            Assert.Equal(0, message.Properties.Count);
        }

        [Fact]
        public void TestTryParseAddressIntoMessagePropertiesFailsNoMatchMultiple()
        {
            IList<string> input = new List<string>() { "a/{b}/d/", "a/{b}/c/{d}/" };
            var config = new MessageAddressConversionConfiguration(
                input,
                DontCareOutput);
            var converter = new MessageAddressConverter(config);

            string address = "a/bee/c/";
            ProtocolGatewayMessage message = new ProtocolGatewayMessage.Builder(Payload, address)
                .Build();
            bool status = converter.TryParseProtocolMessagePropsFromAddress(message);
            Assert.False(status);
            Assert.Equal(0, message.Properties.Count);
        }

        [Fact]
        public void TestTryParseAddressWithParamsIntoMessageProperties()
        {
            IList<string> input = new List<string>() { "a/{b}/{c}/d/{params}/" };
            var config = new MessageAddressConversionConfiguration(
                input,
                DontCareOutput);
            var converter = new MessageAddressConverter(config);

            string address = "a/bee/cee/d/p1=v1&p2=v2&$.mid=mv1/";
            ProtocolGatewayMessage message = new ProtocolGatewayMessage.Builder(Payload, address)
                .Build();
            bool status = converter.TryParseProtocolMessagePropsFromAddress(message);
            Assert.True(status);
            Assert.Equal(5, message.Properties.Count);
            Assert.Equal("bee", message.Properties["b"]);
            Assert.Equal("cee", message.Properties["c"]);
            Assert.Equal("v1", message.Properties["p1"]);
            Assert.Equal("v2", message.Properties["p2"]);
            Assert.Equal("mv1", message.Properties["$.mid"]);
        }

        [Fact]
        public void TestTryParseAddressWithParamsIntoMessagePropertiesMultipleInput()
        {
            IList<string> input = new List<string>() { "a/{b}/c/{params}/", "a/{b}/{c}/d/{params}/" };
            var config = new MessageAddressConversionConfiguration(
                input,
                DontCareOutput);
            var converter = new MessageAddressConverter(config);

            string address = "a/bee/cee/d/p1=v1&p2=v2&$.mid=mv1/";
            ProtocolGatewayMessage message = new ProtocolGatewayMessage.Builder(Payload, address)
                .Build();
            bool status = converter.TryParseProtocolMessagePropsFromAddress(message);
            Assert.True(status);
            Assert.Equal(5, message.Properties.Count);
            Assert.Equal("bee", message.Properties["b"]);
            Assert.Equal("cee", message.Properties["c"]);
            Assert.Equal("v1", message.Properties["p1"]);
            Assert.Equal("v2", message.Properties["p2"]);
            Assert.Equal("mv1", message.Properties["$.mid"]);
        }

        [Fact]
        public void TestTryParseAddressWithParamsIntoMessagePropertiesFailsNoMatch()
        {
            IList<string> input = new List<string>() { "a/{b}/c/{params}/" };
            var config = new MessageAddressConversionConfiguration(
                input,
                DontCareOutput);
            var converter = new MessageAddressConverter(config);

            string address = "a/bee/c/";
            ProtocolGatewayMessage message = new ProtocolGatewayMessage.Builder(Payload, address)
                .Build();
            bool status = converter.TryParseProtocolMessagePropsFromAddress(message);
            Assert.False(status);
            Assert.Equal(0, message.Properties.Count);
        }

        [Fact]
        public void TestTryParseAddressWithParamsIntoMessagePropertiesFailsNoMatchMultiple()
        {
            IList<string> input = new List<string>() { "a/{b}/d/", "a/{b}/c/{params}" };
            var config = new MessageAddressConversionConfiguration(
                input,
                DontCareOutput);
            var converter = new MessageAddressConverter(config);

            string address = "a/bee/p1=v1&p2=v2";
            ProtocolGatewayMessage message = new ProtocolGatewayMessage.Builder(Payload, address)
                .Build();
            bool status = converter.TryParseProtocolMessagePropsFromAddress(message);
            Assert.False(status);
            Assert.Equal(0, message.Properties.Count);
        }

        [Fact]
        public void TestTryParseAddressWithTailingSlashOptional()
        {
            IList<string> input = new List<string> { "a/{b}/c/{d}", "a/{b}/c/{d}/" };
            var config = new MessageAddressConversionConfiguration(
                input,
                DontCareOutput);
            var converter = new MessageAddressConverter(config);

            string address = "a/bee/c/dee";
            ProtocolGatewayMessage message = new ProtocolGatewayMessage.Builder(Payload, address)
                .Build();
            bool status = converter.TryParseProtocolMessagePropsFromAddress(message);
            Assert.True(status);
            Assert.Equal(2, message.Properties.Count);
            Assert.Equal("bee", message.Properties["b"]);
            Assert.Equal("dee", message.Properties["d"]);

            string address2 = "a/bee/c/dee/";
            ProtocolGatewayMessage message2 = new ProtocolGatewayMessage.Builder(Payload, address2)
                .Build();
            bool status2 = converter.TryParseProtocolMessagePropsFromAddress(message2);
            Assert.True(status2);
            Assert.Equal(2, message2.Properties.Count);
            Assert.Equal("bee", message2.Properties["b"]);
            Assert.Equal("dee", message2.Properties["d"]);
        }

        [Fact]
        public void TestTryParseAddressWithTailingSlashOptionalWithMultipleMatches()
        {
            IList<string> input = new List<string>
            {
                "a/{b}/c/{d}/{e}/",
                "a/{b}/c/{d}/",
                "a/{b}/c/",
                "a/{b}/c/{d}/{e}",
                "a/{b}/c/{d}",
                "a/{b}/c"
            };
            var config = new MessageAddressConversionConfiguration(
                input,
                DontCareOutput);
            var converter = new MessageAddressConverter(config);

            string address = "a/bee/c/";
            ProtocolGatewayMessage message = new ProtocolGatewayMessage.Builder(Payload, address)
                .Build();
            bool status = converter.TryParseProtocolMessagePropsFromAddress(message);
            Assert.True(status);
            Assert.Equal(1, message.Properties.Count);
            Assert.Equal("bee", message.Properties["b"]);

            string address2 = "a/bee/c/dee/";
            ProtocolGatewayMessage message2 = new ProtocolGatewayMessage.Builder(Payload, address2)
                .Build();
            bool status2 = converter.TryParseProtocolMessagePropsFromAddress(message2);
            Assert.True(status2);
            Assert.Equal(2, message2.Properties.Count);
            Assert.Equal("bee", message2.Properties["b"]);
            Assert.Equal("dee", message2.Properties["d"]);

            string address3 = "a/bee/c/dee";
            ProtocolGatewayMessage message3 = new ProtocolGatewayMessage.Builder(Payload, address3)
                .Build();
            bool status3 = converter.TryParseProtocolMessagePropsFromAddress(message3);
            Assert.True(status2);
            Assert.Equal(2, message2.Properties.Count);
            Assert.Equal("bee", message2.Properties["b"]);
            Assert.Equal("dee", message2.Properties["d"]);
        }

        static Mock<IMessage> CreateMessageWithSystemProps(IDictionary<string, string> props)
        {
            var message = new Mock<IMessage>();
            message.SetupGet(msg => msg.SystemProperties).Returns(props);
            return message;
        }
    }
}
