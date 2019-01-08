// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt.Test
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Web;
    using DotNetty.Buffers;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt;
    using Moq;
    using Xunit;
    using Constants = Microsoft.Azure.Devices.Edge.Hub.Mqtt.Constants;
    using IProtocolGatewayMessage = Microsoft.Azure.Devices.ProtocolGateway.Messaging.IMessage;

    [Unit]
    public class ProtocolGatewayMessageConverterTest
    {
        static readonly IByteBufferConverter ByteBufferConverter = new ByteBufferConverter(PooledByteBufferAllocator.Default);
        static readonly IByteBuffer Payload = new ByteBufferConverter(PooledByteBufferAllocator.Default).ToByteBuffer(new byte[] { 1, 2, 3 });

        [Fact]
        public void TestToMessage()
        {
            var outputTemplates = new Dictionary<string, string>
            {
                ["Dummy"] = string.Empty
            };
            var inputTemplates = new List<string>
            {
                "devices/{deviceId}/messages/events/{params}/",
                "devices/{deviceId}/messages/events/"
            };
            var config = new MessageAddressConversionConfiguration(
                inputTemplates,
                outputTemplates);
            var converter = new MessageAddressConverter(config);
            var properties = new Dictionary<string, string>();
            var protocolGatewayMessage = Mock.Of<IProtocolGatewayMessage>(
                m =>
                    m.Address == @"devices/Device_6/messages/events/%24.cid=Corrid1&%24.mid=MessageId1&Foo=Bar&Prop2=Value2&Prop3=Value3/" &&
                    m.Payload.Equals(Payload) &&
                    m.Properties == properties);

            var protocolGatewayMessageConverter = new ProtocolGatewayMessageConverter(converter, ByteBufferConverter);
            IMessage message = protocolGatewayMessageConverter.ToMessage(protocolGatewayMessage);
            Assert.NotNull(message);

            Assert.Equal(3, message.SystemProperties.Count);
            Assert.Equal("Corrid1", message.SystemProperties[SystemProperties.MsgCorrelationId]);
            Assert.Equal("MessageId1", message.SystemProperties[SystemProperties.MessageId]);
            Assert.Equal("Device_6", message.SystemProperties[SystemProperties.ConnectionDeviceId]);

            Assert.Equal(3, message.Properties.Count);
            Assert.Equal("Bar", message.Properties["Foo"]);
            Assert.Equal("Value2", message.Properties["Prop2"]);
            Assert.Equal("Value3", message.Properties["Prop3"]);
        }

        [Fact]
        public void TestToMessage_Module()
        {
            var outputTemplates = new Dictionary<string, string>
            {
                ["Dummy"] = string.Empty
            };
            var inputTemplates = new List<string>
            {
                "devices/{deviceId}/messages/events/{params}/",
                "devices/{deviceId}/messages/events/",
                "devices/{deviceId}/modules/{moduleId}/messages/events/{params}/",
                "devices/{deviceId}/modules/{moduleId}/messages/events/"
            };
            var config = new MessageAddressConversionConfiguration(
                inputTemplates,
                outputTemplates);
            var converter = new MessageAddressConverter(config);
            var properties = new Dictionary<string, string>();
            var protocolGatewayMessage = Mock.Of<IProtocolGatewayMessage>(
                m =>
                    m.Address == @"devices/Device_6/modules/SensorModule/messages/events/%24.cid=Corrid1&%24.mid=MessageId1&Foo=Bar&Prop2=Value2&Prop3=Value3/" &&
                    m.Payload.Equals(Payload) &&
                    m.Properties == properties);

            var protocolGatewayMessageConverter = new ProtocolGatewayMessageConverter(converter, ByteBufferConverter);
            IMessage message = protocolGatewayMessageConverter.ToMessage(protocolGatewayMessage);
            Assert.NotNull(message);

            Assert.Equal(4, message.SystemProperties.Count);
            Assert.Equal("Corrid1", message.SystemProperties[SystemProperties.MsgCorrelationId]);
            Assert.Equal("MessageId1", message.SystemProperties[SystemProperties.MessageId]);
            Assert.Equal("Device_6", message.SystemProperties[SystemProperties.ConnectionDeviceId]);
            Assert.Equal("SensorModule", message.SystemProperties[SystemProperties.ConnectionModuleId]);

            Assert.Equal(3, message.Properties.Count);
            Assert.Equal("Bar", message.Properties["Foo"]);
            Assert.Equal("Value2", message.Properties["Prop2"]);
            Assert.Equal("Value3", message.Properties["Prop3"]);
        }

        [Fact]
        public void TestToMessage_AllSystemProperties()
        {
            var outputTemplates = new Dictionary<string, string>
            {
                ["Dummy"] = string.Empty
            };
            var inputTemplates = new List<string>
            {
                "devices/{deviceId}/messages/events/{params}/",
                "devices/{deviceId}/messages/events/"
            };
            var config = new MessageAddressConversionConfiguration(
                inputTemplates,
                outputTemplates);
            var converter = new MessageAddressConverter(config);
            var properties = new Dictionary<string, string>();

            var address = new StringBuilder();
            // base address
            address.Append("devices/Device_6/messages/events/");

            // append all system properties
            // correlation id
            address.Append($"{HttpUtility.UrlEncode("$.cid")}=1234");
            // message id
            address.Append($"&{HttpUtility.UrlEncode("$.mid")}=mid1");
            // to
            address.Append($"&{HttpUtility.UrlEncode("$.to")}=d2");
            // userId
            address.Append($"&{HttpUtility.UrlEncode("$.uid")}=u1");
            // output name
            address.Append($"&{HttpUtility.UrlEncode("$.on")}=op1");
            // contentType
            address.Append($"&{HttpUtility.UrlEncode("$.ct")}={HttpUtility.UrlEncode("application/json")}");
            // content encoding
            address.Append($"&{HttpUtility.UrlEncode("$.ce")}={HttpUtility.UrlEncode("utf-8")}");
            // messageSchema
            address.Append($"&{HttpUtility.UrlEncode("$.schema")}=someschema");
            // creation time
            address.Append($"&{HttpUtility.UrlEncode("$.ctime")}={HttpUtility.UrlEncode("2018-01-31")}");

            // add custom properties
            address.Append("&Foo=Bar&Prop2=Value2&Prop3=Value3/");

            var protocolGatewayMessage = Mock.Of<IProtocolGatewayMessage>(
                m =>
                    m.Address == address.ToString() &&
                    m.Payload.Equals(Payload) &&
                    m.Properties == properties);

            var protocolGatewayMessageConverter = new ProtocolGatewayMessageConverter(converter, ByteBufferConverter);
            IMessage message = protocolGatewayMessageConverter.ToMessage(protocolGatewayMessage);
            Assert.NotNull(message);

            Assert.Equal(10, message.SystemProperties.Count);
            Assert.Equal("1234", message.SystemProperties[SystemProperties.MsgCorrelationId]);
            Assert.Equal("mid1", message.SystemProperties[SystemProperties.MessageId]);
            Assert.Equal("d2", message.SystemProperties[SystemProperties.To]);
            Assert.Equal("u1", message.SystemProperties[SystemProperties.UserId]);
            Assert.Equal("op1", message.SystemProperties[SystemProperties.OutputName]);
            Assert.Equal("application/json", message.SystemProperties[SystemProperties.ContentType]);
            Assert.Equal("utf-8", message.SystemProperties[SystemProperties.ContentEncoding]);
            Assert.Equal("someschema", message.SystemProperties[SystemProperties.MessageSchema]);
            Assert.Equal("2018-01-31", message.SystemProperties[SystemProperties.CreationTime]);
            Assert.Equal("Device_6", message.SystemProperties[SystemProperties.ConnectionDeviceId]);

            Assert.Equal(3, message.Properties.Count);
            Assert.Equal("Bar", message.Properties["Foo"]);
            Assert.Equal("Value2", message.Properties["Prop2"]);
            Assert.Equal("Value3", message.Properties["Prop3"]);
        }

        [Fact]
        public void TestFromMessage_AllSystemProperties()
        {
            const string DeviceId = "Device1";
            const string ModuleId = "Module1";
            const string Input = "input1";
            var outputTemplates = new Dictionary<string, string>
            {
                ["ModuleEndpoint"] = "devices/{deviceId}/modules/{moduleId}/inputs/{inputName}"
            };
            var inputTemplates = new List<string>
            {
                "devices/{deviceId}/messages/events/{params}/",
                "devices/{deviceId}/messages/events/"
            };
            var config = new MessageAddressConversionConfiguration(
                inputTemplates,
                outputTemplates);
            var converter = new MessageAddressConverter(config);

            var properties = new Dictionary<string, string>
            {
                ["Foo"] = "Bar",
                ["Prop2"] = "Value2",
                ["Prop3"] = "Value3"
            };

            var systemProperties = new Dictionary<string, string>
            {
                [SystemProperties.OutboundUri] = Constants.OutboundUriModuleEndpoint,
                [SystemProperties.LockToken] = Guid.NewGuid().ToString(),
                [TemplateParameters.DeviceIdTemplateParam] = DeviceId,
                [Constants.ModuleIdTemplateParameter] = ModuleId,
                [SystemProperties.InputName] = Input,
                [SystemProperties.OutputName] = "output",
                [SystemProperties.ContentEncoding] = "utf-8",
                [SystemProperties.ContentType] = "application/json",
                [SystemProperties.MessageSchema] = "schema1",
                [SystemProperties.To] = "foo",
                [SystemProperties.UserId] = "user1",
                [SystemProperties.MsgCorrelationId] = "1234",
                [SystemProperties.MessageId] = "m1",
                [SystemProperties.ConnectionDeviceId] = "fromDevice1",
                [SystemProperties.ConnectionModuleId] = "fromModule1"
            };

            var message = Mock.Of<IMessage>(
                m =>
                    m.Body == new byte[] { 1, 2, 3 } &&
                    m.Properties == properties &&
                    m.SystemProperties == systemProperties);

            var protocolGatewayMessageConverter = new ProtocolGatewayMessageConverter(converter, ByteBufferConverter);
            IProtocolGatewayMessage pgMessage = protocolGatewayMessageConverter.FromMessage(message);
            Assert.NotNull(pgMessage);
            Assert.Equal(@"devices/Device1/modules/Module1/inputs/input1/Foo=Bar&Prop2=Value2&Prop3=Value3&%24.ce=utf-8&%24.ct=application%2Fjson&%24.schema=schema1&%24.to=foo&%24.uid=user1&%24.cid=1234&%24.mid=m1&%24.cdid=fromDevice1&%24.cmid=fromModule1", pgMessage.Address);
            Assert.Equal(12, pgMessage.Properties.Count);
            Assert.Equal("Bar", pgMessage.Properties["Foo"]);
            Assert.Equal("Value2", pgMessage.Properties["Prop2"]);
            Assert.Equal("Value3", pgMessage.Properties["Prop3"]);
            Assert.Equal("utf-8", pgMessage.Properties["$.ce"]);
            Assert.Equal("application/json", pgMessage.Properties["$.ct"]);
            Assert.Equal("schema1", pgMessage.Properties["$.schema"]);
            Assert.Equal("foo", pgMessage.Properties["$.to"]);
            Assert.Equal("user1", pgMessage.Properties["$.uid"]);
            Assert.Equal("1234", pgMessage.Properties["$.cid"]);
            Assert.Equal("m1", pgMessage.Properties["$.mid"]);
            Assert.Equal("fromDevice1", pgMessage.Properties["$.cdid"]);
            Assert.Equal("fromModule1", pgMessage.Properties["$.cmid"]);
            Assert.False(pgMessage.Properties.ContainsKey("$.on"));
        }

        [Fact]
        public void TestFromMessage_DuplicateSystemProperties()
        {
            const string DeviceId = "Device1";
            const string ModuleId = "Module1";
            const string Input = "input1";
            var outputTemplates = new Dictionary<string, string>
            {
                ["ModuleEndpoint"] = "devices/{deviceId}/modules/{moduleId}/inputs/{inputName}"
            };
            var inputTemplates = new List<string>
            {
                "devices/{deviceId}/messages/events/{params}/",
                "devices/{deviceId}/messages/events/"
            };
            var config = new MessageAddressConversionConfiguration(
                inputTemplates,
                outputTemplates);
            var converter = new MessageAddressConverter(config);

            var properties = new Dictionary<string, string>
            {
                ["Foo"] = "Bar",
                ["Prop2"] = "Value2",
                ["Prop3"] = "Value3",
                ["$.cdid"] = "IncorrectDeviceId",
                ["$.cmid"] = "IncorrectModuleId"
            };

            var systemProperties = new Dictionary<string, string>
            {
                [SystemProperties.OutboundUri] = Constants.OutboundUriModuleEndpoint,
                [SystemProperties.LockToken] = Guid.NewGuid().ToString(),
                [TemplateParameters.DeviceIdTemplateParam] = DeviceId,
                [Constants.ModuleIdTemplateParameter] = ModuleId,
                [SystemProperties.InputName] = Input,
                [SystemProperties.OutputName] = "output",
                [SystemProperties.ContentEncoding] = "utf-8",
                [SystemProperties.ContentType] = "application/json",
                [SystemProperties.MessageSchema] = "schema1",
                [SystemProperties.To] = "foo",
                [SystemProperties.UserId] = "user1",
                [SystemProperties.MsgCorrelationId] = "1234",
                [SystemProperties.MessageId] = "m1",
                [SystemProperties.ConnectionDeviceId] = "fromDevice1",
                [SystemProperties.ConnectionModuleId] = "fromModule1"
            };

            var message = Mock.Of<IMessage>(
                m =>
                    m.Body == new byte[] { 1, 2, 3 } &&
                    m.Properties == properties &&
                    m.SystemProperties == systemProperties);

            var protocolGatewayMessageConverter = new ProtocolGatewayMessageConverter(converter, ByteBufferConverter);
            IProtocolGatewayMessage pgMessage = protocolGatewayMessageConverter.FromMessage(message);
            Assert.NotNull(pgMessage);
            Assert.Equal(
                @"devices/Device1/modules/Module1/inputs/input1/Foo=Bar&Prop2=Value2&Prop3=Value3&%24.cdid=fromDevice1&%24.cmid=fromModule1&%24.ce=utf-8&%24.ct=application%2Fjson&%24.schema=schema1&%24.to=foo&%24.uid=user1&%24.cid=1234&%24.mid=m1",
                pgMessage.Address);
            Assert.Equal(12, pgMessage.Properties.Count);
            Assert.Equal("Bar", pgMessage.Properties["Foo"]);
            Assert.Equal("Value2", pgMessage.Properties["Prop2"]);
            Assert.Equal("Value3", pgMessage.Properties["Prop3"]);
            Assert.Equal("utf-8", pgMessage.Properties["$.ce"]);
            Assert.Equal("application/json", pgMessage.Properties["$.ct"]);
            Assert.Equal("schema1", pgMessage.Properties["$.schema"]);
            Assert.Equal("foo", pgMessage.Properties["$.to"]);
            Assert.Equal("user1", pgMessage.Properties["$.uid"]);
            Assert.Equal("1234", pgMessage.Properties["$.cid"]);
            Assert.Equal("m1", pgMessage.Properties["$.mid"]);
            Assert.Equal("fromDevice1", pgMessage.Properties["$.cdid"]);
            Assert.Equal("fromModule1", pgMessage.Properties["$.cmid"]);
            Assert.False(pgMessage.Properties.ContainsKey("$.on"));
        }

        [Fact]
        public void TestToMessage_NoTopicMatch()
        {
            var outputTemplates = new Dictionary<string, string>
            {
                ["Dummy"] = string.Empty
            };
            var inputTemplates = new List<string>
            {
                "devices/{deviceId}/messages/events/{params}/",
                "devices/{deviceId}/messages/events/"
            };
            var config = new MessageAddressConversionConfiguration(
                inputTemplates,
                outputTemplates);
            var converter = new MessageAddressConverter(config);
            var properties = new Dictionary<string, string>();
            var protocolGatewayMessage = Mock.Of<IProtocolGatewayMessage>(
                m =>
                    m.Address == @"devices/Device_6/messages/eve/%24.cid=Corrid1&%24.mid=MessageId1&Foo=Bar&Prop2=Value2&Prop3=Value3/" &&
                    m.Payload.Equals(Payload) &&
                    m.Properties == properties);

            var protocolGatewayMessageConverter = new ProtocolGatewayMessageConverter(converter, ByteBufferConverter);
            Assert.Throws<InvalidOperationException>(() => protocolGatewayMessageConverter.ToMessage(protocolGatewayMessage));
        }
    }
}
