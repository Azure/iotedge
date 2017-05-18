namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt.Test
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Unit]
    public class MessageAddressConverterTest
    {
        static readonly IList<string> Input = new List<string>() { "devices/{deviceId}/messages/events/"};
        static readonly IList<string> MultipleAddress = new List<string>() { "devices/{deviceId}/messages/events/", "devices/{deviceId}/messages/events/" };
        static readonly IList<string> Output = new List<string>() { "devices/{deviceId}/messages/devicebound", "$iothub/twin/res/{statusCode}/?$rid={correlationId}" };
        static readonly MessageAddressConversionConfiguration ConversionConfig = new MessageAddressConversionConfiguration(Input, Output);
        static readonly MessageAddressConversionConfiguration EmptyConversionConfig = new MessageAddressConversionConfiguration();
        static readonly MessageAddressConversionConfiguration MultipleInputConversionConfig = new MessageAddressConversionConfiguration(MultipleAddress, Output);
        static readonly string[] DontCare = new[] { "" };

        [Fact]
        public void TestMessageAddressConverterWithEmptyConversionConfig()
        {
            Assert.Throws(typeof(ArgumentException), () => new MessageAddressConverter(EmptyConversionConfig));
        }

        [Fact]
        public void TryDeriveAddressWorksWithOneTemplate()
        {
            string address;
            var config = new MessageAddressConversionConfiguration(
                DontCare,
                new[] { "a/{b}/c" }
            );
            var properties = new Dictionary<string, string>()
            {
                ["b"] = "123"
            };

            var converter = new MessageAddressConverter(config);
            bool result = converter.TryDeriveAddress(properties, out address);

            Assert.True(result);
            Assert.NotNull(address);
            Assert.NotEmpty(address);
            Assert.Equal<string>("a/123/c", address);
        }

        [Fact]
        public void TryDeriveAddressWorksWithMoreThanOneTemplate()
        {
            string address;
            var config = new MessageAddressConversionConfiguration(
                DontCare,
                new[] { "a/{b}/c", "d/{e}/f", "x/{y}/z" }
            );
            var properties = new Dictionary<string, string>()
            {
                ["e"] = "123"
            };

            var converter = new MessageAddressConverter(config);
            bool result = converter.TryDeriveAddress(properties, out address);

            Assert.True(result);
            Assert.NotNull(address);
            Assert.NotEmpty(address);
            Assert.Equal<string>("d/123/f", address);
        }

        [Fact]
        public void TryDeriveAddressUsesTheFirstMatch()
        {
            string address;
            var config = new MessageAddressConversionConfiguration(
                DontCare,
                new[] { "a/{b}/c", "{b}/c/d" }
            );
            var properties = new Dictionary<string, string>()
            {
                ["b"] = "123"
            };

            var converter = new MessageAddressConverter(config);
            bool result = converter.TryDeriveAddress(properties, out address);

            Assert.True(result);
            Assert.NotNull(address);
            Assert.NotEmpty(address);
            Assert.Equal<string>("a/123/c", address);
        }

        [Fact]
        public void TestTryDeriveAddressWithoutDeviceId()
        {
            var converter = new MessageAddressConverter(ConversionConfig);

            IDictionary<string, string> properties = new Dictionary<string, string>()
            {
                { "model", "temperature" }
            };
            string address;
            converter.TryDeriveAddress(properties, out address);

            Assert.Null(address);
        }

        [Fact]
        public void TestTryParseAddressIntoMessageProperties()
        {
            var converter = new MessageAddressConverter(ConversionConfig);

            string address = "devices/{deviceId}/messages/events/";
            DotNetty.Buffers.IByteBuffer payload = new byte[] { 1, 2, 3}.ToByteBuffer();
            var message = new ProtocolGatewayMessage(payload, address);
            bool status = converter.TryParseAddressIntoMessageProperties(address, message);

            Assert.True(status);
        }

        [Fact]
        public void TestTryParseAddressIntoMessagePropertiesNoMatch()
        {
            var converter = new MessageAddressConverter(ConversionConfig);

            string address = "devices/{deviceId}/messages/";
            DotNetty.Buffers.IByteBuffer payload = new byte[] { 1, 2, 3 }.ToByteBuffer();
            var message = new ProtocolGatewayMessage(payload, address + "events/");
            bool status = converter.TryParseAddressIntoMessageProperties(address, message);

            Assert.False(status);
        }

        [Fact]
        public void TestTryParseAddressIntoMessagePropertiesMultipleMatch()
        {
            var converter = new MessageAddressConverter(MultipleInputConversionConfig);

            string address = "devices/{deviceId}/messages/events/";
            DotNetty.Buffers.IByteBuffer payload = new byte[] { 1, 2, 3 }.ToByteBuffer();
            var message = new ProtocolGatewayMessage(payload, address + "events/");
            bool status = converter.TryParseAddressIntoMessageProperties(address, message);

            Assert.True(status);

            
        }
    }
}
