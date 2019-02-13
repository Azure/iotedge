// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Test
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core.MessageSources;
    using Microsoft.Azure.Devices.Routing.Core.Query.Types;
    using Newtonsoft.Json;
    using Xunit;

    public class MessageQueryTest : RoutingUnitTestBase
    {
        const string MessageBody =
            @"{
               ""message"": {
                  ""Weather"": {
                     ""Temperature"": 50,
                     ""Location"": {
                        ""Street"": ""One Microsoft Way"",
                        ""City"": ""Redmond"",
                        ""State"": ""WA""
                                }
                  }
               }
            }";

        static readonly IMessage BodyQueryMessageUtf8ValidJson = new Message(
            TelemetryMessageSource.Instance,
            Encoding.UTF8.GetBytes(MessageBody),
            new Dictionary<string, string>(),
            new Dictionary<string, string>
            {
                { SystemProperties.ContentEncoding, "UTF-8" },
                { SystemProperties.ContentType, Constants.SystemPropertyValues.ContentType.Json },
            });

        static readonly IMessage BodyQueryMessageUtf16ValidJson = new Message(
            TelemetryMessageSource.Instance,
            Encoding.Unicode.GetBytes(MessageBody),
            new Dictionary<string, string>(),
            new Dictionary<string, string>
            {
                { SystemProperties.ContentEncoding, "UTf-16" },
                { SystemProperties.ContentType, Constants.SystemPropertyValues.ContentType.Json },
            });

        static readonly IMessage BodyQueryMessageUtf32ValidJson = new Message(
            TelemetryMessageSource.Instance,
            Encoding.UTF32.GetBytes(MessageBody),
            new Dictionary<string, string>(),
            new Dictionary<string, string>
            {
                { SystemProperties.ContentEncoding, "utf-32" },
                { SystemProperties.ContentType, Constants.SystemPropertyValues.ContentType.Json },
            });

        static readonly IMessage BodyQueryMessageUtf8InvalidJson = new Message(
            TelemetryMessageSource.Instance,
            Encoding.UTF8.GetBytes("Invalid Json"),
            new Dictionary<string, string>(),
            new Dictionary<string, string>
            {
                { SystemProperties.ContentEncoding, "UTF-8" },
                { SystemProperties.ContentType, Constants.SystemPropertyValues.ContentType.Json },
            });

        static readonly IMessage BodyQueryMessageInvalidEncoding = new Message(
            TelemetryMessageSource.Instance,
            new byte[] { 1, 2, 3 },
            new Dictionary<string, string>(),
            new Dictionary<string, string>
            {
                { SystemProperties.ContentEncoding, "UTF-8" },
                { SystemProperties.ContentType, Constants.SystemPropertyValues.ContentType.Json },
            });

        static readonly IMessage BodyQueryMessageUtf8ValidJsonMissingEncodingProperty = new Message(
            TelemetryMessageSource.Instance,
            Encoding.UTF8.GetBytes(MessageBody),
            new Dictionary<string, string>(),
            new Dictionary<string, string>
            {
                { SystemProperties.ContentType, Constants.SystemPropertyValues.ContentType.Json },
            });

        static readonly IMessage BodyQueryMessageUtf8ValidJsonMissingContentTypeProperty = new Message(
            TelemetryMessageSource.Instance,
            Encoding.UTF8.GetBytes(MessageBody),
            new Dictionary<string, string>(),
            new Dictionary<string, string>
            {
                { SystemProperties.ContentEncoding, "Utf-8" },
            });

        [Fact]
        [Unit]
        public void TestMessageQuery()
        {
            Assert.Equal(
                new QueryValue(50, QueryValueType.Double),
                BodyQueryMessageUtf8ValidJson.GetQueryValue("message.Weather.Temperature"));

            Assert.Equal(
                new QueryValue(50, QueryValueType.Double),
                BodyQueryMessageUtf16ValidJson.GetQueryValue("message.Weather.Temperature"));

            Assert.Equal(
                new QueryValue(50, QueryValueType.Double),
                BodyQueryMessageUtf32ValidJson.GetQueryValue("message.Weather.Temperature"));

            Assert.Throws<JsonReaderException>(
                () => BodyQueryMessageUtf8InvalidJson.GetQueryValue("message.Weather.Temperature"));

            Assert.Throws<JsonReaderException>(
                () => BodyQueryMessageInvalidEncoding.GetQueryValue("message.Weather.Temperature"));

            Assert.Throws<InvalidOperationException>(
                () => BodyQueryMessageUtf8ValidJsonMissingEncodingProperty.GetQueryValue("message.Weather.Temperature"));

            Assert.Throws<InvalidOperationException>(
                () => BodyQueryMessageUtf8ValidJsonMissingContentTypeProperty.GetQueryValue("message.Weather.Temperature"));
        }
    }
}
