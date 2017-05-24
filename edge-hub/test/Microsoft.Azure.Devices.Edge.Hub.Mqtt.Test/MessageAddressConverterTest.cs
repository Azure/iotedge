namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt.Test
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Unit]
    public class MessageAddressConverterTest
    {
        static readonly string[] DontCareInput = new[] { "" };

        static readonly IDictionary<string, string> DontCareOutput = new Dictionary<string, string>
        {
            ["DontCare"] = ""
        };
        static readonly DotNetty.Buffers.IByteBuffer Payload = new byte[] { 1, 2, 3 }.ToByteBuffer();

        [Fact]
        public void TestMessageAddressConverterWithEmptyConversionConfig()
        {
            var emptyConversionConfig = new MessageAddressConversionConfiguration();
            Assert.Throws(typeof(ArgumentException), () => new MessageAddressConverter(emptyConversionConfig));
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
                testTemplate
            );
            var properties = new Dictionary<string, string>()
            {
                ["b"] = "123"
            };

            var converter = new MessageAddressConverter(config);
            bool result = converter.TryDeriveAddress("Test", properties, out address);

            Assert.True(result);
            Assert.NotNull(address);
            Assert.NotEmpty(address);
            Assert.Equal<string>("a/123/c", address);
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
                testTemplate
            );
            var properties = new Dictionary<string, string>()
            {
                ["e"] = "123"
            };

            var converter = new MessageAddressConverter(config);
            bool result = converter.TryDeriveAddress("Test2", properties, out address);

            Assert.True(result);
            Assert.NotNull(address);
            Assert.NotEmpty(address);
            Assert.Equal<string>("d/123/f", address);
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
                testTemplate
            );
            var properties = new Dictionary<string, string>()
            {
                ["b"] = "123",
                ["d"] = "456",
                ["f"] = "789"
            };

            var converter = new MessageAddressConverter(config);
            bool result = converter.TryDeriveAddress("Test", properties, out address);

            Assert.True(result);
            Assert.NotNull(address);
            Assert.NotEmpty(address);
            Assert.Equal<string>("a/123/c/456/e/789/", address);
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
                testTemplate
            );
            var properties = new Dictionary<string, string>()
            {
                ["b"] = "123"
            };

            var converter = new MessageAddressConverter(config);
            bool result = converter.TryDeriveAddress("BadTest", properties, out address);

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
                testTemplate
            );
            var properties = new Dictionary<string, string>();

            var converter = new MessageAddressConverter(config);
            bool result = converter.TryDeriveAddress("Test", properties, out address);

            Assert.False(result);
            Assert.Null(address);
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
                testTemplate
            );
            var properties = new Dictionary<string, string>()
            {
                ["a"] = "123"
            };

            var converter = new MessageAddressConverter(config);
            bool result = converter.TryDeriveAddress("Test", properties, out address);

            Assert.False(result);
            Assert.Null(address);
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
                testTemplate
            );
            var properties = new Dictionary<string, string>()
            {
                ["b"] = "123",
                ["f"] = "789",
            };

            var converter = new MessageAddressConverter(config);
            bool result = converter.TryDeriveAddress("Test", properties, out address);

            Assert.False(result);
            Assert.Null(address);
        }

        [Fact]
        public void TestTryParseAddressIntoMessageProperties()
        {
            IList<string> input = new List<string>() { "a/{b}/c/{d}/" };
            var config = new MessageAddressConversionConfiguration(
                input,
                DontCareOutput
            );
            var converter = new MessageAddressConverter(config);

            string address = "a/bee/c/dee/";
            var message = new ProtocolGatewayMessage(Payload, address);
            bool status = converter.TryParseAddressIntoMessageProperties(address, message);
            Assert.True(status);
            string value;
            Assert.True(message.Properties.TryGetValue("b", out value));
            Assert.Equal<string>("bee", value);
            Assert.True(message.Properties.TryGetValue("d", out value));
            Assert.Equal<string>("dee", value);
        }

        [Fact]
        public void TestTryParseAddressIntoMessagePropertiesMultipleInput()
        {
            IList<string> input = new List<string>() { "a/{b}/c/{d}/", "e/{f}/g/{h}/" };
            var config = new MessageAddressConversionConfiguration(
                input,
                DontCareOutput
            );
            var converter = new MessageAddressConverter(config);

            string address = "a/bee/c/dee/";
            var message = new ProtocolGatewayMessage(Payload, address);
            bool status = converter.TryParseAddressIntoMessageProperties(address, message);
            Assert.True(status);
            string value;
            Assert.True(message.Properties.TryGetValue("b", out value));
            Assert.Equal<string>("bee", value);
            Assert.True(message.Properties.TryGetValue("d", out value));
            Assert.Equal<string>("dee", value);
            Assert.Equal<int>(2, message.Properties.Count);
        }

        [Fact]
        public void TestTryParseAddressIntoMessagePropertiesFailsNoMatch()
        {
            IList<string> input = new List<string>() { "a/{b}/c/{d}/" };
            var config = new MessageAddressConversionConfiguration(
                input,
                DontCareOutput
            );
            var converter = new MessageAddressConverter(config);

            string address = "a/bee/c/";
            var message = new ProtocolGatewayMessage(Payload, address);
            bool status = converter.TryParseAddressIntoMessageProperties(address, message);
            Assert.False(status);
            Assert.Equal<int>(0, message.Properties.Count);
        }

        [Fact]
        public void TestTryParseAddressIntoMessagePropertiesFailsNoMatchMultiple()
        {
            IList<string> input = new List<string>() { "a/{b}/d/", "a/{b}/c/{d}/" };
            var config = new MessageAddressConversionConfiguration(
                input,
                DontCareOutput
            );
            var converter = new MessageAddressConverter(config);

            string address = "a/bee/c/";
            var message = new ProtocolGatewayMessage(Payload, address);
            bool status = converter.TryParseAddressIntoMessageProperties(address, message);
            Assert.False(status);
            Assert.Equal<int>(0, message.Properties.Count);
        }
    }
}
