namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt.Test
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    public class MessageAddressConverterTest
    {
        static readonly IList<string> input = new List<string>() { "devices/{deviceId}/messages/events/"};
        static readonly IList<string> multipleAddress = new List<string>() { "devices/{deviceId}/messages/events/", "devices/{deviceId}/messages/events/" };
        static readonly IList<string> output = new List<string>() { "devices/{deviceId}/messages/devicebound" };
        static readonly MessageAddressConversionConfiguration conversionConfig = new MessageAddressConversionConfiguration(input, output);
        static readonly MessageAddressConversionConfiguration emptyConversionConfig = new MessageAddressConversionConfiguration();
        static readonly MessageAddressConversionConfiguration multipleInputConversionConfig = new MessageAddressConversionConfiguration(multipleAddress, output);

        [Fact]
        [Unit]
        public void TestTryDeriveAddressValidAddress()
        {
            var converter = new MessageAddressConverter(conversionConfig);

            IDictionary<string, string> properties = new Dictionary<string, string>()
            {
                { "deviceId", "device1" }
            };
            string address;
            converter.TryDeriveAddress(properties, out address);

            Assert.NotNull(address);
            Assert.NotEmpty(address);
            Assert.Equal<string>(address, "devices/device1/messages/devicebound");
        }

        [Fact]
        [Unit]
        public void TestTryDeriveAddressWithoutDeviceId()
        {
            var converter = new MessageAddressConverter(conversionConfig);

            IDictionary<string, string> properties = new Dictionary<string, string>()
            {
                { "model", "temperature" }
            };
            string address;
            converter.TryDeriveAddress(properties, out address);

            Assert.Null(address);
        }

        [Fact]
        [Unit]
        public void TestTryDeriveAddressWithEmptyConversionConfig()
        {
            Assert.Throws(typeof(ArgumentException), () => new MessageAddressConverter(emptyConversionConfig));
        }


        [Fact]
        [Unit]
        public void TestTryParseAddressIntoMessageProperties()
        {
            var converter = new MessageAddressConverter(conversionConfig);

            IDictionary<string, string> properties = new Dictionary<string, string>()
            {
                { "deviceId", "device1" }
            };
            string address = "devices/{deviceId}/messages/events/";
            DotNetty.Buffers.IByteBuffer payload = new byte[] { 1, 2, 3}.ToByteBuffer();
            var message = new ProtocolGatewayMessage(payload, address);
            bool status = converter.TryParseAddressIntoMessageProperties(address, message);

            Assert.True(status);
        }

        [Fact]
        [Unit]
        public void TestTryParseAddressIntoMessagePropertiesNoMatch()
        {
            var converter = new MessageAddressConverter(conversionConfig);

            IDictionary<string, string> properties = new Dictionary<string, string>()
            {
                { "deviceId", "device1" }
            };
            string address = "devices/{deviceId}/messages/";
            DotNetty.Buffers.IByteBuffer payload = new byte[] { 1, 2, 3 }.ToByteBuffer();
            var message = new ProtocolGatewayMessage(payload, address + "events/");
            bool status = converter.TryParseAddressIntoMessageProperties(address, message);

            Assert.False(status);
        }

        [Fact]
        [Unit]
        public void TestTryParseAddressIntoMessagePropertiesMultipleMatch()
        {
            var converter = new MessageAddressConverter(multipleInputConversionConfig);

            IDictionary<string, string> properties = new Dictionary<string, string>()
            {
                { "deviceId", "device1" }
            };
            string address = "devices/{deviceId}/messages/events/";
            DotNetty.Buffers.IByteBuffer payload = new byte[] { 1, 2, 3 }.ToByteBuffer();
            var message = new ProtocolGatewayMessage(payload, address + "events/");
            bool status = converter.TryParseAddressIntoMessageProperties(address, message);

            Assert.True(status);

            
        }
    }
}
